﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="CatiaProductToElementDefinitionRule.cs" company="RHEA System S.A.">
//    Copyright (c) 2020-2021 RHEA System S.A.
// 
//    Author: Sam Gerené, Alex Vorobiev, Alexander van Delft, Nathanael Smiechowski.
// 
//    This file is part of DEHCATIA
// 
//    The DEHCATIA is free software; you can redistribute it and/or
//    modify it under the terms of the GNU Lesser General Public
//    License as published by the Free Software Foundation; either
//    version 3 of the License, or (at your option) any later version.
// 
//    The DEHCATIA is distributed in the hope that it will be useful,
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
    using System.Linq;
    using System.Runtime.ExceptionServices;

    using Autofac;

    using CDP4Common.EngineeringModelData;
    using CDP4Common.SiteDirectoryData;
    using CDP4Common.Types;

    using DEHCATIA.DstController;
    using DEHCATIA.Enumerations;
    using DEHCATIA.Services.ParameterTypeService;
    using DEHCATIA.ViewModels.ProductTree.Rows;

    using DEHPCommon;
    using DEHPCommon.HubController.Interfaces;
    using DEHPCommon.MappingRules.Core;

    using NLog;

    /// <summary>
    /// Rule definition that transforms a collection of <see cref="ElementRowViewModel"/> to a collection <see cref="ElementDefinition"/>
    /// </summary>
    public class CatiaProductToElementRule : MappingRule<ElementRowViewModel, List<(ElementRowViewModel Parent, ElementBase Element)>>
    {
        /// <summary>
        /// The current class logger
        /// </summary>
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// The <see cref="IHubController"/>
        /// </summary>
        private readonly IHubController hubController = AppContainer.Container.Resolve<IHubController>();

        /// <summary>
        /// The <see cref="IDstController"/>
        /// </summary>
        private IDstController dstController;
        
        /// <summary>
        /// The <see cref="IParameterTypeService"/>
        /// </summary>
        private readonly IParameterTypeService parameterTypeService = AppContainer.Container.Resolve<IParameterTypeService>();

        /// <summary>
        /// The collection of <see cref="ElementBase"/> that results of the <see cref="Transform"/>
        /// </summary>
        private readonly List<(ElementRowViewModel Parent, ElementBase Element)> ruleOutput = new List<(ElementRowViewModel Parent, ElementBase Element)>();

        /// <summary>
        /// The selected <see cref="ActualFiniteState"/> to update the corresponding value sets when updating a state dependant parameter
        /// </summary>
        private ActualFiniteState selectedActualFiniteState;

        /// <summary>
        /// The selected <see cref="Option"/> to update the corresponding value sets when updating a option dependant parameter
        /// </summary>
        private Option selectedOption;

        /// <summary>
        /// The current <see cref="DomainOfExpertise"/>
        /// </summary>
        private DomainOfExpertise owner;

        /// <summary>
        /// Transforms <see cref="!:TInput" /> to a <see cref="!:TOutput" />
        /// </summary>
        public override List<(ElementRowViewModel Parent, ElementBase Element)> Transform(ElementRowViewModel input)
        {
            try
            {
                this.ruleOutput.Clear();

                this.owner = this.hubController.CurrentDomainOfExpertise;
                this.dstController = AppContainer.Container.Resolve<IDstController>();

                this.Map(new List<ElementRowViewModel>{input});

                return this.ruleOutput;
            }
            catch (Exception exception)
            {
                Logger.Error(exception);
                ExceptionDispatchInfo.Capture(exception).Throw();
                return default;
            }
        }

        /// <summary>
        /// Maps the <paramref name="input"/>
        /// </summary>
        /// <param name="input">The collection of <see cref="ElementRowViewModel"/></param>
        private void Map(IEnumerable<ElementRowViewModel> input)
        {
            foreach (var elementRowViewModel in input)
            {
                this.selectedActualFiniteState ??= elementRowViewModel.SelectedActualFiniteState;
                this.selectedOption ??= elementRowViewModel.SelectedOption;

                if (elementRowViewModel.ElementType == ElementType.CatPart
                    && elementRowViewModel is UsageRowViewModel usageRow
                    && usageRow.Children.FirstOrDefault() is DefinitionRowViewModel definitionRow)
                {
                    usageRow.ElementDefinition = this.MapDefinitionRowViewModel(definitionRow);
                    this.MapElementUsage(usageRow);
                    this.MapParameters(usageRow);
                    this.dstController.SaveElementMapping(usageRow);
                    this.ruleOutput.Add((usageRow.Parent, usageRow.ElementDefinition));
                    this.ruleOutput.Add((usageRow.Parent, usageRow.ElementUsage));
                }
                else
                {
                    this.MapElementRowViewModel(elementRowViewModel);
                    this.MapParameters(elementRowViewModel);

                    if (elementRowViewModel.Parent is { } parent)
                    {
                        var elementUsage = this.GetOrCreateElementUsage(elementRowViewModel.ElementDefinition, elementRowViewModel.Name, parent.ElementDefinition);
                        this.MapParameters(elementUsage, elementRowViewModel);
                        this.ruleOutput.Add((parent, elementUsage));
                    }

                    this.Map(elementRowViewModel.Children);
                }
            }
        }

        /// <summary>
        /// Maps the <see cref="ElementRowViewModel"/>
        /// </summary>
        /// <param name="elementRowViewModel">The <see cref="ElementRowViewModel"/> to map</param>
        /// <returns>The <see cref="ElementDefinition"/> mapped to the <see cref="ElementRowViewModel"/></returns>
        private ElementDefinition MapDefinitionRowViewModel(DefinitionRowViewModel elementRowViewModel)
        {
            if (this.ruleOutput.FirstOrDefault(x => x.Element.ShortName == elementRowViewModel.Name).Element is ElementDefinition elementDefinition)
            {
                elementRowViewModel.ElementDefinition = elementDefinition;
            }
            else
            {
                this.MapElementRowViewModel(elementRowViewModel);
            }

            this.MapParameters(elementRowViewModel);
            this.MapShape(elementRowViewModel);

            return elementRowViewModel.ElementDefinition;
        }

        /// <summary>
        /// Maps the relevant parameter if the shape is comaptible
        /// </summary>
        /// <param name="definitionRow">The <see cref="DefinitionRowViewModel"/></param>
        private void MapShape(DefinitionRowViewModel definitionRow)
        {
            if (definitionRow.Shape.IsSupported)
            {
                this.MapParameter(this.parameterTypeService.ShapeKind, ParameterTypeService.ShapeKindShortName, definitionRow, definitionRow.Shape.ShapeKind.ToString());
                this.MapParameter(this.parameterTypeService.ShapeLength, ParameterTypeService.ShapeLengthShortName, definitionRow, definitionRow.Shape.Length?.Value?.Value);
                this.MapParameter(this.parameterTypeService.ShapeWidthOrDiameter, ParameterTypeService.ShapeWidthOrDiameterShortName, definitionRow, definitionRow.Shape.WidthOrDiameter?.Value?.Value);
                this.MapParameter(this.parameterTypeService.ShapeHeight, ParameterTypeService.ShapeHeightShortName, definitionRow, definitionRow.Shape.Height?.Value?.Value);
                this.MapParameter(this.parameterTypeService.ShapeSupportLength, ParameterTypeService.ShapeSupportLengthShortName, definitionRow, definitionRow.Shape.LengthSupport?.Value?.Value);
                this.MapParameter(this.parameterTypeService.ShapeAngle, ParameterTypeService.ShapeAngleShortName, definitionRow, definitionRow.Shape.Angle?.Value?.Value);
                this.MapParameter(this.parameterTypeService.ShapeSupportAngle, ParameterTypeService.ShapeSupportAngleShortName, definitionRow, definitionRow.Shape.AngleSupport?.Value?.Value);
                this.MapParameter(this.parameterTypeService.ShapeThickness, ParameterTypeService.ShapeThicknessShortName, definitionRow, definitionRow.Shape.Thickness?.Value?.Value);
                this.MapParameter(this.parameterTypeService.ShapeArea, ParameterTypeService.ShapeAreaShortName, definitionRow, definitionRow.Shape.Area?.Value?.Value);
                this.MapParameter(this.parameterTypeService.ShapeDensity, ParameterTypeService.ShapeDensityShortName, definitionRow, definitionRow.Shape.Density?.Value?.Value);
                this.MapParameter(this.parameterTypeService.ShapeMassMargin, ParameterTypeService.ShapeMassMarginShortName, definitionRow, definitionRow.Shape.MassMargin?.Value?.Value);
                this.MapParameter(this.parameterTypeService.ShapeMassWithMargin, ParameterTypeService.ShapeMassWithMarginShortName, definitionRow, definitionRow.Shape.MassWithMargin?.Value?.Value);
                this.MapParameter(this.parameterTypeService.ShapeSysMassMargin, ParameterTypeService.ShapeSysMassMarginShortName, definitionRow, definitionRow.Shape.SysMassMargin?.Value?.Value);
            }
        }

        /// <summary>
        /// Maps the <see cref="ElementRowViewModel"/>
        /// </summary>
        /// <param name="elementRowViewModel">The <see cref="ElementRowViewModel"/> to map</param>
        /// <returns>The <see cref="ElementDefinition"/> mapped to the <see cref="ElementRowViewModel"/></returns>
        private void MapElementRowViewModel(ElementRowViewModel elementRowViewModel)
        {
            this.MapElementDefinition(elementRowViewModel);
            this.ruleOutput.Add((elementRowViewModel.Parent, elementRowViewModel.ElementDefinition));
            this.dstController.SaveElementMapping(elementRowViewModel);
        }

        /// <summary>
        /// Maps the <paramref name="elementRow"/> to a <see cref="ElementDefinition"/>
        /// </summary>
        /// <param name="elementRow">The <see cref="ElementRowViewModel"/> to map</param>
        private void MapElementDefinition(ElementRowViewModel elementRow)
        {
            elementRow.ElementDefinition ??= this.hubController.OpenIteration.Element
                                                    .FirstOrDefault(x => x.ShortName == elementRow.Name)?.Clone(true)
                                                ?? new ElementDefinition()
                                                {
                                                    Name = elementRow.Name,
                                                    ShortName = elementRow.Name,
                                                    Owner = this.hubController.CurrentDomainOfExpertise,
                                                    Container = this.hubController.OpenIteration
                                                };
        }

        /// <summary>
        /// Maps the <paramref name="usageRow"/> to a <see cref="ElementUsage"/>
        /// </summary>
        /// <param name="usageRow">The <see cref="UsageRowViewModel"/> to map</param>
        private void MapElementUsage(UsageRowViewModel usageRow)
        {
            usageRow.ElementUsage ??= this.GetOrCreateElementUsage(usageRow.ElementDefinition, usageRow.Name, usageRow.Parent.ElementDefinition);
        }

        /// <summary>
        /// Looks for an existing <see cref="ElementUsage"/> or returns a new one
        /// </summary>
        /// <param name="elementDefinition">The <see cref="ElementDefinition"/></param>
        /// <param name="name">The name of the <see cref="ElementUsage"/></param>
        /// <param name="parent">The parent <see cref="ElementDefinition"/></param>
        /// <returns>An <see cref="ElementUsage"/></returns>
        private ElementUsage GetOrCreateElementUsage(ElementDefinition elementDefinition, string name, ElementDefinition parent)
        {
            var container = this.hubController.OpenIteration.Element
                .FirstOrDefault(x => x.ShortName == parent?.ShortName);

            var elementUsage = container?.ContainedElement
                .FirstOrDefault(x => x.ShortName == name)?.Clone(true);

            if (elementUsage is null)
            {
                elementUsage = new ElementUsage()
                {
                    Name = name,
                    ShortName = name,
                    ElementDefinition = elementDefinition,
                    Owner = this.hubController.CurrentDomainOfExpertise
                };

                parent?.ContainedElement.Add(elementUsage);
            }

            return elementUsage;
        }

        /// <summary>
        /// Maps the parameters of the <paramref name="elementUsage"/>
        /// </summary>
        /// <param name="elementUsage">The <see cref="ElementUsage"/></param>
        /// <param name="elementRowViewModel">The <see cref="ElementRowViewModel"/></param>
        private void MapParameters(ElementUsage elementUsage, ElementRowViewModel elementRowViewModel)
        {
            this.MapParameterOverride(this.parameterTypeService.CenterOfGravity,
                ParameterTypeService.CenterOfGravityShortName, elementUsage, elementRowViewModel.ElementDefinition, elementRowViewModel.CenterOfGravity.Values);

            this.MapParameterOverride(this.parameterTypeService.MomentOfInertia,
                ParameterTypeService.MomentOfInertiaShortName, elementUsage, elementRowViewModel.ElementDefinition, elementRowViewModel.MomentOfInertia.Value.Values);

            this.MapParameterOverride(this.parameterTypeService.Volume,
                ParameterTypeService.VolumeShortName, elementUsage, elementRowViewModel.ElementDefinition, elementRowViewModel.Volume.Value.Value);

            this.MapParameterOverride(this.parameterTypeService.Mass,
                ParameterTypeService.MassShortName, elementUsage, elementRowViewModel.ElementDefinition, elementRowViewModel.Mass.Value.Value);

            this.MapParameterOverride(this.parameterTypeService.Orientation,
                ParameterTypeService.OrientationShortName, elementUsage, elementRowViewModel.ElementDefinition, elementRowViewModel.Shape.PositionOrientation.Orientation.Values);

            this.MapParameterOverride(this.parameterTypeService.Position,
                ParameterTypeService.PositionShortName, elementUsage, elementRowViewModel.ElementDefinition, elementRowViewModel.Shape.PositionOrientation.Position.Values);
        }

        /// <summary>
        /// Maps the parameters of the <paramref name="definitionRow"/>
        /// </summary>
        /// <param name="definitionRow">The <see cref="DefinitionRowViewModel"/></param>
        private void MapParameters(DefinitionRowViewModel definitionRow)
        {
            this.MapParameters((ElementRowViewModel)definitionRow);
        }

        /// <summary>
        /// Maps the parameters of the <paramref name="usageRow"/>
        /// </summary>
        /// <param name="usageRow">The <see cref="UsageRowViewModel"/></param>
        private void MapParameters(UsageRowViewModel usageRow)
        {
            this.MapParameterOverride(this.parameterTypeService.CenterOfGravity,
                ParameterTypeService.CenterOfGravityShortName, usageRow.ElementUsage, usageRow.ElementDefinition, usageRow.CenterOfGravity.Values);

            this.MapParameterOverride(this.parameterTypeService.MomentOfInertia,
                ParameterTypeService.MomentOfInertiaShortName, usageRow.ElementUsage, usageRow.ElementDefinition, usageRow.MomentOfInertia.Value.Values);

            this.MapParameterOverride(this.parameterTypeService.Volume,
                ParameterTypeService.VolumeShortName, usageRow.ElementUsage, usageRow.ElementDefinition, usageRow.Volume.Value.Value);

            this.MapParameterOverride(this.parameterTypeService.Mass,
                ParameterTypeService.MassShortName, usageRow.ElementUsage, usageRow.ElementDefinition, usageRow.Mass.Value.Value);

            this.MapParameterOverride(this.parameterTypeService.Orientation,
                ParameterTypeService.OrientationShortName, usageRow.ElementUsage, usageRow.ElementDefinition, usageRow.Shape.PositionOrientation.Orientation.Values);

            this.MapParameterOverride(this.parameterTypeService.Position,
                ParameterTypeService.PositionShortName, usageRow.ElementUsage, usageRow.ElementDefinition, usageRow.Shape.PositionOrientation.Position.Values);
        }

        /// <summary>
        /// Maps the parameters of the <paramref name="elementRow"/>
        /// </summary>
        /// <param name="elementRow">The <see cref="ElementRowViewModel"/></param>
        private void MapParameters(ElementRowViewModel elementRow)
        {
            this.MapParameter(this.parameterTypeService.CenterOfGravity,
                            ParameterTypeService.CenterOfGravityShortName, elementRow, elementRow.CenterOfGravity.Values);

            this.MapParameter(this.parameterTypeService.MomentOfInertia,
                ParameterTypeService.MomentOfInertiaShortName, elementRow, elementRow.MomentOfInertia.Value.Values);

            this.MapParameter(this.parameterTypeService.Volume,
                ParameterTypeService.VolumeShortName, elementRow, elementRow.Volume.Value?.Value);

            this.MapParameter(this.parameterTypeService.Mass,
                ParameterTypeService.MassShortName, elementRow, elementRow.Mass.Value?.Value);

            this.MapParameter(this.parameterTypeService.Orientation,
                ParameterTypeService.OrientationShortName, elementRow, elementRow.Shape.PositionOrientation.Orientation.Values);

            this.MapParameter(this.parameterTypeService.Position,
                ParameterTypeService.PositionShortName, elementRow, elementRow.Shape.PositionOrientation.Position.Values);
        }

        /// <summary>
        /// Maps the provided <paramref name="value"/> to a parameter of type <paramref name="parameterType"/>
        /// </summary>
        /// <param name="parameterTypeShortName">The short name of the <paramref name="parameterType"/></param>
        /// <param name="definitionRow">The <see cref="DefinitionRowViewModel"/>The <see cref="DefinitionRowViewModel"/></param>
        /// <param name="parameterType">The current <see cref="ParameterType"/></param>
        /// <param name="value">The value <see cref="double"/></param>
        private void MapParameter(ParameterType parameterType, string parameterTypeShortName, ElementRowViewModel definitionRow, double? value)
        {
            this.MapParameter(parameterType, parameterTypeShortName, definitionRow,
                value.HasValue ? new[] { value.Value } : new double[] { });
        }

        /// <summary>
        /// Maps the provided <paramref name="values"/> to a parameter of type <paramref name="parameterType"/>
        /// </summary>
        /// <param name="parameterTypeShortName">The short name of the <paramref name="parameterType"/></param>
        /// <param name="definitionRow">The <see cref="DefinitionRowViewModel"/>The <see cref="DefinitionRowViewModel"/></param>
        /// <param name="parameterType">The current <see cref="ParameterType"/></param>
        /// <param name="values">array of <see cref="double"/> that contains the actual values to be mapped</param>
        private void MapParameter(ParameterType parameterType, string parameterTypeShortName, ElementRowViewModel definitionRow, params double[] values)
        {
            if (parameterType is null || values.Length < 1)
            {
                Logger.Info($"The {parameterTypeShortName} parameter has been skipped for the {definitionRow.Name}");
                return;
            }

            var parameter = this.GetParameter(parameterType, parameterTypeShortName, definitionRow, values.Length);
            
            this.UpdateValueSet(parameter, values);

            Logger.Info($"The {parameterTypeShortName} parameter has been updated for the {definitionRow.Name}");
        }

        /// <summary>
        /// Maps the provided <paramref name="values"/> to a parameter of type <paramref name="parameterType"/>
        /// </summary>
        /// <param name="parameterTypeShortName">The short name of the <paramref name="parameterType"/></param>
        /// <param name="definitionRow">The <see cref="DefinitionRowViewModel"/>The <see cref="DefinitionRowViewModel"/></param>
        /// <param name="parameterType">The current <see cref="ParameterType"/></param>
        /// <param name="values">array of <see cref="string"/> that contains the actual values to be mapped</param>
        private void MapParameter(ParameterType parameterType, string parameterTypeShortName, ElementRowViewModel definitionRow, params string[] values)
        {
            if (parameterType is null || values.Length < 1)
            {
                Logger.Info($"The {parameterTypeShortName} parameter has been skipped for the {definitionRow.Name}");
                return;
            }

            var parameter = this.GetParameter(parameterType, parameterTypeShortName, definitionRow, values.Length);
            
            this.UpdateValueSet(parameter, values);

            Logger.Info($"The {parameterTypeShortName} parameter has been updated for the {definitionRow.Name}");
        }

        /// <summary>
        /// Gets or creates a new Parameter based on the <paramref name="parameterType"/>
        /// </summary>
        /// <param name="parameterType">The <see cref="ParameterType"/></param>
        /// <param name="parameterTypeShortName">The <paramref name="parameterType"/> ShortName</param>
        /// <param name="definitionRow">The <see cref="ElementRowViewModel"/></param>
        /// <param name="numberOfValue">The number of values the parameter will hold</param>
        /// <returns>A <see cref="Parameter"/></returns>
        private Parameter GetParameter(ParameterType parameterType, string parameterTypeShortName, ElementRowViewModel definitionRow, int numberOfValue)
        {
            Parameter parameter;

            if (definitionRow.ElementDefinition.Parameter
                .FirstOrDefault(p => p.ParameterType.ShortName == parameterTypeShortName) is { } existingParameter)
            {
                parameter = existingParameter.Clone(true);
            }
            else
            {
                var initializationCollection = this.CreateValueArrayInitializationCollection(numberOfValue);

                parameter = new Parameter(Guid.Empty, this.hubController.Session.Assembler.Cache, new Uri(this.hubController.Session.DataSourceUri))
                {
                    ParameterType = parameterType,
                    Owner = this.owner,
                    ValueSet =
                    {
                        new ParameterValueSet(Guid.Empty, this.hubController.Session.Assembler.Cache, new Uri(this.hubController.Session.DataSourceUri))
                        {
                            Computed = new ValueArray<string>(),
                            Formula = new ValueArray<string>(initializationCollection),
                            Manual = new ValueArray<string>(initializationCollection),
                            Reference = new ValueArray<string>(initializationCollection),
                            Published = new ValueArray<string>(initializationCollection)
                        }
                    }
                };

                if (parameterType is QuantityKind quantityKind)
                {
                    parameter.Scale = quantityKind.DefaultScale;
                }

                definitionRow.ElementDefinition.Parameter.Add(parameter);
            }

            return parameter;
        }

        /// <summary>
        /// Maps the provided <paramref name="values"/> to a parameter of type <paramref name="parameterType"/>
        /// </summary>
        /// <param name="parameterTypeShortName">The short name of the <paramref name="parameterType"/></param>
        /// <param name="elementUsage">The <see cref="ElementUsage"/></param>
        /// <param name="parameterType">The current <see cref="ParameterType"/></param>
        /// <param name="elementDefinition">The <see cref="ElementDefinition"/></param>
        /// <param name="values">array of <see cref="double"/> that contains the actual values to be mapped</param>
        private void MapParameterOverride(ParameterType parameterType, string parameterTypeShortName, ElementUsage elementUsage, ElementDefinition elementDefinition, params double[] values)
        {
            if (!(parameterType is { }))
            {
                Logger.Info($"The {parameterTypeShortName} parameter override has been skipped for the {elementUsage.Name}");
                return;
            }

            ParameterOverride parameter;

            if (elementUsage.ParameterOverride
                .FirstOrDefault(p => p.ParameterType.ShortName == parameterTypeShortName) is { } existingParameter)
            {
                parameter = existingParameter;
            }
            else
            {
                var initializationCollection = this.CreateValueArrayInitializationCollection(values.Length);

                var parameterToOverride = elementDefinition.Parameter.FirstOrDefault(x => x.ParameterType.ShortName == parameterTypeShortName);

                parameter = new ParameterOverride(Guid.Empty, this.hubController.Session.Assembler.Cache, new Uri(this.hubController.Session.DataSourceUri))
                {
                    Owner = this.owner,
                    Parameter = parameterToOverride,
                    ValueSet =
                    {
                        new ParameterOverrideValueSet(Guid.Empty, this.hubController.Session.Assembler.Cache, new Uri(this.hubController.Session.DataSourceUri))
                        {
                            ParameterValueSet = (ParameterValueSet)parameterToOverride?.QueryParameterBaseValueSet(this.selectedOption, this.selectedActualFiniteState),
                            Computed = new ValueArray<string>(),
                            Formula = new ValueArray<string>(initializationCollection),
                            Manual = new ValueArray<string>(initializationCollection),
                            Reference = new ValueArray<string>(initializationCollection),
                            Published = new ValueArray<string>(initializationCollection)
                        }
                    }
                };

                elementUsage.ParameterOverride.Add(parameter);
            }

            this.UpdateValueSet(parameter, values);

            Logger.Info($"The {parameterTypeShortName} parameter has been updated for the {elementUsage.Name}");
        }

        /// <summary>
        /// Creates a array of string for <see cref="IValueSet"/> initialization collection
        /// </summary>
        /// <param name="numberOfValue">The number of default value to be generated</param>
        /// <returns>An array of string</returns>
        private List<string> CreateValueArrayInitializationCollection(int numberOfValue)
        {
            var values = new List<string>();

            for (var number = 0; number < numberOfValue; number++)
            {
                values.Add("-");
            }

            return values;
        }

        /// <summary>
        /// Updates the <paramref name="parameter"/> correct valueset with the <paramref name="values"/>
        /// </summary>
        /// <param name="parameter">The <see cref="Parameter"/> to update</param>
        /// <param name="values">The collection of string to assign to the <paramref name="parameter"/></param>
        private void UpdateValueSet(ParameterBase parameter, params double[] values)
        {
            this.UpdateValueSet(parameter, values.Select(x => FormattableString.Invariant($"{x}")));
        }

        /// <summary>
        /// Updates the <paramref name="parameter"/> correct valueset with the <paramref name="values"/>
        /// </summary>
        /// <param name="parameter">The <see cref="Parameter"/> to update</param>
        /// <param name="values">The collection of string to assign to the <paramref name="parameter"/></param>
        private void UpdateValueSet(ParameterBase parameter, IEnumerable<string> values)
        {
            var valueSet = parameter.QueryParameterBaseValueSet(this.selectedOption, this.selectedActualFiniteState);

            ((ParameterValueSetBase)valueSet).Computed = new ValueArray<string>(values);

            valueSet.ValueSwitch = ParameterSwitchKind.COMPUTED;
        }
    }
}
