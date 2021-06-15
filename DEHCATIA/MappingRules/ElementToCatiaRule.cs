﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ElementToCatiaRule.cs" company="RHEA System S.A.">
//    Copyright (c) 2020-2021 RHEA System S.A.
// 
//    Author: Sam Gerené, Alex Vorobiev, Alexander van Delft, Nathanael Smiechowski.
// 
//    This file is part of DEHPEcosimPro
// 
//    The DEHPEcosimPro is free software; you can redistribute it and/or
//    modify it under the terms of the GNU Lesser General Public
//    License as published by the Free Software Foundation; either
//    version 3 of the License, or (at your option) any later version.
// 
//    The DEHPEcosimPro is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
//    Lesser General Public License for more details.
// 
//    You should have received a copy of the GNU Lesser General Public License
//    along with this program; if not, write to the Free Software Foundation,
//    Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace DEHCATIA.MappingRules
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Runtime.ExceptionServices;

    using Autofac;

    using CDP4Common.EngineeringModelData;
    using CDP4Common.Helpers;
    using CDP4Common.SiteDirectoryData;

    using DEHCATIA.Enumerations;
    using DEHCATIA.Services.CatiaTemplateService;
    using DEHCATIA.Services.ParameterTypeService;
    using DEHCATIA.ViewModels.ProductTree.Parameters;
    using DEHCATIA.ViewModels.ProductTree.Rows;
    using DEHCATIA.ViewModels.ProductTree.Shapes;
    using DEHCATIA.ViewModels.Rows;

    using DEHPCommon;
    using DEHPCommon.HubController.Interfaces;
    using DEHPCommon.MappingRules.Core;

    using NLog;

    /// <summary>
    /// Rule definition that transforms a collection of <see cref="MappedElementRowViewModel"/> to a collection <see cref="ElementRowViewModel"/>
    /// </summary>
    public class ElementToCatiaRule : MappingRule<IEnumerable<MappedElementRowViewModel>, IEnumerable<MappedElementRowViewModel>>
    {
        /// <summary>
        /// The current class logger
        /// </summary>
        private readonly Logger logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// The <see cref="IHubController"/>
        /// </summary>
        private readonly IHubController hubController = AppContainer.Container.Resolve<IHubController>();

        /// <summary>
        /// The <see cref="IParameterTypeService"/>
        /// </summary>
        private readonly IParameterTypeService parameterTypeService = AppContainer.Container.Resolve<IParameterTypeService>();

        /// <summary>
        /// The <see cref="ICatiaTemplateService"/>
        /// </summary>
        private readonly ICatiaTemplateService catiaTemplateService = AppContainer.Container.Resolve<ICatiaTemplateService>();

        /// <summary>
        /// Gets the collection of error messages from <see cref="mappingErrors"/>
        /// </summary>
        public IReadOnlyList<string> MappingErrors => this.mappingErrors.AsReadOnly();

        /// <summary>
        /// The private collection of mapping errors
        /// </summary>
        private readonly List<string> mappingErrors = new List<string>();

        /// <summary>
        /// Transforms <see cref="MappedElementRowViewModel" /> to a <see cref="ElementRowViewModel" />
        /// </summary>
        public override IEnumerable<MappedElementRowViewModel> Transform(IEnumerable<MappedElementRowViewModel> input)
        {
            try
            {
                var mappedElementRowViewModels = input as MappedElementRowViewModel[] ?? input.ToArray();

                this.Map(mappedElementRowViewModels);

                return mappedElementRowViewModels;
            }
            catch (Exception exception)
            {
                this.logger.Error(exception);
                ExceptionDispatchInfo.Capture(exception).Throw();
                return default;
            }
        }

        /// <summary>
        /// Maps the <see cref="input"/>
        /// </summary>
        /// <param name="input">The collection of <see cref="MappedElementRowViewModel"/> containing the informations about the things that were mapped</param>
        private void Map(IEnumerable<MappedElementRowViewModel> input)
        {
            foreach (var mappedElementRowViewModel in input)
            {
                if (mappedElementRowViewModel.ShouldCreateNewElement
                    && mappedElementRowViewModel.CatiaParent != null
                    && !this.CreateCatiaElement(mappedElementRowViewModel))
                {
                    continue;
                }

                this.MapPosition(mappedElementRowViewModel);
                this.MapOrientation(mappedElementRowViewModel);
                this.MapParameters(mappedElementRowViewModel);
            }
        }

        /// <summary>
        /// Maps the parameters of <see cref="MappedElementRowViewModel.HubElement"/> from <paramref name="mappedElementRowViewModel"/>
        /// </summary>
        /// <param name="mappedElementRowViewModel">The <see cref="MappedElementRowViewModel"/></param>
        private void MapParameters(MappedElementRowViewModel mappedElementRowViewModel)
        {
            var parameters = this.GetParameters(mappedElementRowViewModel.HubElement).ToArray();

            if (mappedElementRowViewModel.CatiaElement.Shape is null
                && this.GetParameterEnumValue<ShapeKind>(parameters, this.parameterTypeService.ShapeKind.Iid, mappedElementRowViewModel)?.FirstOrDefault() is { } shapeKind)
            {
                mappedElementRowViewModel.CatiaElement.Shape = new CatiaShapeViewModel(true) { ShapeKind = shapeKind };
            }
            else
            {
                var message = $"The shape kind described in the element {mappedElementRowViewModel.HubElement.Name} isn't supported by either Catia or the adapter";
                this.mappingErrors.Add(message);
                this.logger.Warn(message);
                return;
            }

            mappedElementRowViewModel.CatiaElement.Shape.Angle =
                this.GetNewParameterValue(mappedElementRowViewModel, parameters, 
                    mappedElementRowViewModel.CatiaElement.Shape.Angle, this.parameterTypeService.ShapeAngle?.Iid);

            mappedElementRowViewModel.CatiaElement.Shape.AngleSupport =
                this.GetNewParameterValue(mappedElementRowViewModel, parameters, 
                    mappedElementRowViewModel.CatiaElement.Shape.AngleSupport, this.parameterTypeService.ShapeSupportAngle?.Iid);

            mappedElementRowViewModel.CatiaElement.Shape.Area = this.GetNewParameterValue(mappedElementRowViewModel, parameters,
                mappedElementRowViewModel.CatiaElement.Shape.Area, this.parameterTypeService.ShapeArea?.Iid);

            mappedElementRowViewModel.CatiaElement.Shape.Density =
                this.GetNewParameterValue(mappedElementRowViewModel, parameters,
                    mappedElementRowViewModel.CatiaElement.Shape.Density, this.parameterTypeService.ShapeDensity?.Iid);

            mappedElementRowViewModel.CatiaElement.Shape.Height =
                this.GetNewParameterValue(mappedElementRowViewModel, parameters,
                    mappedElementRowViewModel.CatiaElement.Shape.Height, this.parameterTypeService.ShapeHeight?.Iid);

            mappedElementRowViewModel.CatiaElement.Shape.WidthOrDiameter =
                this.GetNewParameterValue(mappedElementRowViewModel, parameters,
                    mappedElementRowViewModel.CatiaElement.Shape.WidthOrDiameter, this.parameterTypeService.ShapeWidthOrDiameter?.Iid);

            mappedElementRowViewModel.CatiaElement.Shape.Length =
                this.GetNewParameterValue(mappedElementRowViewModel, parameters,
                    mappedElementRowViewModel.CatiaElement.Shape.Length, this.parameterTypeService.ShapeLength?.Iid);

            mappedElementRowViewModel.CatiaElement.Shape.LengthSupport =
                this.GetNewParameterValue(mappedElementRowViewModel, parameters,
                    mappedElementRowViewModel.CatiaElement.Shape.LengthSupport, this.parameterTypeService.ShapeSupportLength?.Iid);

            mappedElementRowViewModel.CatiaElement.Shape.Mass =
                this.GetNewParameterValue(mappedElementRowViewModel, parameters,
                    mappedElementRowViewModel.CatiaElement.Shape.Mass, this.parameterTypeService.Mass?.Iid);

            mappedElementRowViewModel.CatiaElement.Shape.MassMargin =
                this.GetNewParameterValue(mappedElementRowViewModel, parameters,
                    mappedElementRowViewModel.CatiaElement.Shape.MassMargin, this.parameterTypeService.ShapeMassMargin?.Iid);

            mappedElementRowViewModel.CatiaElement.Shape.MassWithMargin =
                this.GetNewParameterValue(mappedElementRowViewModel, parameters,
                    mappedElementRowViewModel.CatiaElement.Shape.MassWithMargin, this.parameterTypeService.ShapeMassWithMargin?.Iid);

            mappedElementRowViewModel.CatiaElement.Shape.SysMassMargin =
                this.GetNewParameterValue(mappedElementRowViewModel, parameters,
                    mappedElementRowViewModel.CatiaElement.Shape.SysMassMargin, this.parameterTypeService.ShapeSysMassMargin?.Iid);

            mappedElementRowViewModel.CatiaElement.Shape.Thickness =
                this.GetNewParameterValue(mappedElementRowViewModel, parameters,
                    mappedElementRowViewModel.CatiaElement.Shape.Thickness, this.parameterTypeService.ShapeThickness?.Iid);

            mappedElementRowViewModel.CatiaElement.Shape.Volume =
                this.GetNewParameterValue(mappedElementRowViewModel, parameters,
                    mappedElementRowViewModel.CatiaElement.Shape.Volume, this.parameterTypeService.Volume?.Iid);

            mappedElementRowViewModel.CatiaElement.Shape.ExternalShape = 
                this.GetParameterValue<string>(parameters, this.parameterTypeService.ExternalShape?.Iid, mappedElementRowViewModel)?
                    .FirstOrDefault()
                ?? mappedElementRowViewModel.CatiaElement.Shape.ExternalShape;
        }

        /// <summary>
        /// Gets the new value out of the right <see cref="ParameterOrOverrideBase"/> from <paramref name="parameters"/> for the <paramref name="parameter"/>
        /// </summary>
        /// <param name="mappedElementRowViewModel">The current <see cref="MappedElementRowViewModel"/></param>
        /// <param name="parameters">The collection of <see cref="ParameterOrOverrideBase"/></param>
        /// <param name="parameter">The <see cref="DoubleWithUnitValueViewModel"/></param>
        /// <param name="parameterTypeIid">The <see cref="Guid"/> Id of the <see cref="ParameterType"/></param>
        /// <returns>An updated <see cref="DoubleWithUnitValueViewModel"/></returns>
        private DoubleWithUnitValueViewModel GetNewParameterValue(MappedElementRowViewModel mappedElementRowViewModel, IEnumerable<ParameterOrOverrideBase> parameters, DoubleWithUnitValueViewModel parameter, Guid? parameterTypeIid)
        {
            var newAngleValue = parameterTypeIid.HasValue 
                ? this.GetParameterValue<double>(parameters, parameterTypeIid.Value, mappedElementRowViewModel)?.FirstOrDefault()
                : null;

            if (!newAngleValue.HasValue)
            {
                return parameter;
            }

            if (mappedElementRowViewModel.CatiaElement.Shape.Angle is null)
            {
                return new DoubleWithUnitValueViewModel(newAngleValue.Value);
            }

            parameter.Value = newAngleValue.Value;
            return parameter;
        }

        /// <summary>
        /// Get the values typed as <typeparamref name="TValueType"/> out of a value set from the <paramref name="parameter"/>
        /// </summary>
        /// <typeparam name="TValueType">The type of the collection of values to return where <typeparamref name="TValueType"/> is <see cref="IConvertible"/></typeparam>
        /// <param name="parameter">The <see cref="ParameterBase"/></param>
        /// <param name="mappedElementRowViewModel">The <see cref="MappedElementRowViewModel"/></param>
        /// <returns>A collection of <typeparamref name="TValueType"/></returns>
        private IEnumerable<TValueType> GetValue<TValueType>(ParameterBase parameter, MappedElementRowViewModel mappedElementRowViewModel) where TValueType : IConvertible
        {
            var valueSet = parameter.QueryParameterBaseValueSet(mappedElementRowViewModel.CatiaElement.SelectedOption, mappedElementRowViewModel.CatiaElement.SelectedActualFiniteState);

            var values = new List<TValueType>();

            foreach (var value in valueSet.ActualValue)
            {
                if (typeof(TValueType) == typeof(double)
                    && ValueSetConverter.TryParseDouble(value, parameter.ParameterType, out var output)
                    && output is TValueType result)
                {
                    values.Add(result);
                }
                else if (value.ToValueSetObject(parameter.ParameterType) is TValueType convertedValue)
                {
                    values.Add(convertedValue);
                }
                else
                {
                    var errorMessage = $"The value {value} from Parameter {parameter.ModelCode()} could not be parsed as {typeof(TValueType).Name}";
                    this.mappingErrors.Add(errorMessage);
                    this.logger.Warn(errorMessage);
                }
            }

            return values;
        }

        /// <summary>
        /// Get the values typed as <typeparamref name="TEnum"/> out of a value set from the <paramref name="parameter"/>
        /// </summary>
        /// <typeparam name="TEnum">The type of the collection of values to return where <typeparamref name="TEnum"/> is <see cref="IConvertible"/></typeparam>
        /// <param name="parameter">The <see cref="ParameterBase"/></param>
        /// <param name="mappedElementRowViewModel">The <see cref="MappedElementRowViewModel"/></param>
        /// <returns>A collection of <typeparamref name="TEnum"/></returns>
        private IEnumerable<TEnum> GetEnumValue<TEnum>(ParameterBase parameter, MappedElementRowViewModel mappedElementRowViewModel) where TEnum : struct, Enum
        {
            var valueSet = parameter.QueryParameterBaseValueSet(mappedElementRowViewModel.CatiaElement.SelectedOption, mappedElementRowViewModel.CatiaElement.SelectedActualFiniteState);

            var values = new List<TEnum>();

            foreach (var value in valueSet.ActualValue)
            {
                if(Enum.TryParse(value, true, out TEnum enumResult))
                {
                    values.Add(enumResult);
                }
                else
                {
                    var errorMessage = $"The value {value} from Parameter {parameter.ModelCode()} could not be parsed as Enum {typeof(TEnum).Name}";
                    this.mappingErrors.Add(errorMessage);
                    this.logger.Warn(errorMessage);
                }
            }

            return values;
        }

        /// <summary>
        /// Get the values typed as <typeparamref name="TValueType"/> out of a value set from one of the <paramref name="parameters"/>
        /// </summary>
        /// <typeparam name="TValueType">The type of the collection of values to return where <typeparamref name="TValueType"/> is <see cref="IConvertible"/></typeparam>
        /// <param name="parameters">The collection of <see cref="ParameterOrOverrideBase"/></param>
        /// <param name="parameterTypeIid">The <see cref="ParameterType"/> Iid</param>
        /// <param name="mappedElementRowViewModel">The <see cref="MappedElementRowViewModel"/></param>
        /// <returns>A collection of <typeparamref name="TValueType"/></returns>
        public IEnumerable<TValueType> GetParameterValue<TValueType>(IEnumerable<ParameterOrOverrideBase> parameters, Guid? parameterTypeIid, MappedElementRowViewModel mappedElementRowViewModel) 
            where TValueType : IConvertible
        {
            var parameter = parameters.FirstOrDefault(x => x.ParameterType.Iid == parameterTypeIid);

            return parameter is null 
                ? new List<TValueType>() 
                : this.GetValue<TValueType>(parameter, mappedElementRowViewModel);
        }

        /// <summary>
        /// Get the values typed as <typeparamref name="TEnum"/> out of a value set from one of the <paramref name="parameters"/>
        /// </summary>
        /// <typeparam name="TEnum">The type of the collection of values to return where <typeparamref name="TEnum"/> is <see cref="Enum"/></typeparam>
        /// <param name="parameters">The collection of <see cref="ParameterOrOverrideBase"/></param>
        /// <param name="parameterTypeIid">The <see cref="ParameterType"/> Iid</param>
        /// <param name="mappedElementRowViewModel">The <see cref="MappedElementRowViewModel"/></param>
        /// <returns>A collection of <typeparamref name="TEnum"/></returns>
        public IEnumerable<TEnum> GetParameterEnumValue<TEnum>(IEnumerable<ParameterOrOverrideBase> parameters, Guid parameterTypeIid, MappedElementRowViewModel mappedElementRowViewModel) 
            where TEnum : struct, Enum
        {
            var parameter = parameters.FirstOrDefault(x => x.ParameterType.Iid == parameterTypeIid);

            return parameter is null 
                ? new List<TEnum>() 
                : this.GetEnumValue<TEnum>(parameter, mappedElementRowViewModel);
        }

        /// <summary>
        /// Gets one parameter out of the <paramref name="hubElement"/> where the parameter type is <see cref="parameterType"/>
        /// </summary>
        /// <param name="hubElement">The <see cref="ElementBase"/></param>
        /// <param name="parameterType">The <see cref="ParameterType"/></param>
        /// <returns>A <see cref="ParameterOrOverrideBase"/></returns>
        private ParameterOrOverrideBase GetParameter(ElementBase hubElement, ParameterType parameterType)
        {
            return this.GetParameters(hubElement)
                .FirstOrDefault(x => x.ParameterType.Iid == parameterType?.Iid);
        }

        /// <summary>
        /// Gets the collection of parameter out of the <paramref name="hubElement"/>
        /// </summary>
        /// <param name="hubElement">The <see cref="ElementBase"/></param>
        /// <returns>A collection of <see cref="ParameterOrOverrideBase"/></returns>
        private IEnumerable<ParameterOrOverrideBase> GetParameters(ElementBase hubElement)
        {
            Func<IEnumerable<ParameterOrOverrideBase>> parameters = hubElement switch
            {
                ElementDefinition elementDefinition => () => elementDefinition.Parameter,
                ElementUsage elementUsage => () =>
                {
                    var parameterOrOveride = new List<ParameterOrOverrideBase>(elementUsage.ParameterOverride);
                    var overridenParameter = elementUsage.ParameterOverride.Select(x => x.Parameter).ToList();
                    parameterOrOveride.AddRange(elementUsage.ElementDefinition.Parameter.Except(overridenParameter).ToList());
                    return parameterOrOveride;
                },
                _ => throw new ArgumentOutOfRangeException(nameof(hubElement))
            };

            return parameters.Invoke();
        }

        /// <summary>
        /// Creates a <see cref="ElementRowViewModel"/> when the <see cref="MappedElementRowViewModel.HubElement"/> does not exist yet in the Catia model
        /// </summary>
        /// <param name="mappedElementRowViewModel">The <see cref="MappedElementRowViewModel"/></param>
        /// <returns>A value indicating whether the Catia element draft has been created</returns>
        private bool CreateCatiaElement(MappedElementRowViewModel mappedElementRowViewModel)
        {
            if (this.catiaTemplateService.TryGetFileName(
                this.GetParameter(mappedElementRowViewModel.HubElement, this.parameterTypeService.ShapeKind),
                mappedElementRowViewModel.CatiaParent.SelectedOption, mappedElementRowViewModel.CatiaParent.SelectedActualFiniteState,
                out var fileName))
            {
                mappedElementRowViewModel.CatiaElement = new ElementRowViewModel(mappedElementRowViewModel.HubElement, fileName);
                return true;
            }

            this.mappingErrors.Add($"No template shape has been found based on the element {mappedElementRowViewModel.HubElement.Name}");
            return false;
        }

        /// <summary>
        /// Maps the position parameter
        /// </summary>
        /// <param name="mappedElementRowViewModel">The <see cref="MappedElementRowViewModel"/></param>
        private void MapPosition(MappedElementRowViewModel mappedElementRowViewModel)
        {
            var valueSet = this.GetParameter(mappedElementRowViewModel.HubElement, this.parameterTypeService.Position)?
                .QueryParameterBaseValueSet(mappedElementRowViewModel.CatiaElement.SelectedOption,
                    mappedElementRowViewModel.CatiaElement.SelectedActualFiniteState);

            if (valueSet?.ActualValue.Count != 3)
            {
                this.mappingErrors.Add($"The position parameter was not found or could not be mapped for the element {mappedElementRowViewModel.HubElement.Name}");
                return;
            }

            var positionMatrix = (Convert.ToDouble(valueSet?.ActualValue[0], CultureInfo.InvariantCulture), 
                Convert.ToDouble(valueSet?.ActualValue[1], CultureInfo.InvariantCulture), 
                Convert.ToDouble(valueSet?.ActualValue[2], CultureInfo.InvariantCulture));

            mappedElementRowViewModel.CatiaElement.Shape.PositionOrientation.Position = new PositionParameterValueViewModel(positionMatrix);
        }

        /// <summary>
        /// Maps the Orientation Parameter
        /// </summary>
        /// <param name="mappedElementRowViewModel">The <see cref="MappedElementRowViewModel"/></param>
        private void MapOrientation(MappedElementRowViewModel mappedElementRowViewModel)
        {
            var valueSet = this.GetParameter(mappedElementRowViewModel.HubElement, this.parameterTypeService.Orientation)?
                .QueryParameterBaseValueSet(mappedElementRowViewModel.CatiaElement.SelectedOption,
                    mappedElementRowViewModel.CatiaElement.SelectedActualFiniteState);

            var orientationMatrix = valueSet?.ActualValue.Select(x => Convert.ToDouble(x, CultureInfo.InvariantCulture));

            if (orientationMatrix is null)
            {
                this.mappingErrors.Add($"The Orientation parameter was not found or could not be mapped for the element {mappedElementRowViewModel.HubElement.Name}");
                return;
            }

            mappedElementRowViewModel.CatiaElement.Shape.PositionOrientation.Orientation = new OrientationViewModel(orientationMatrix.ToList());
        }
    }
}
