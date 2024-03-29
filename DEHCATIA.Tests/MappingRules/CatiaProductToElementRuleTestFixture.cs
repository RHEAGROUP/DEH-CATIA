﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="CatiaProductToElementDefinitionRuleTestFixture.cs" company="RHEA System S.A.">
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

namespace DEHCATIA.Tests.MappingRules
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Autofac;

    using CDP4Common.EngineeringModelData;
    using CDP4Common.SiteDirectoryData;

    using CDP4Dal;

    using DEHCATIA.DstController;
    using DEHCATIA.Enumerations;
    using DEHCATIA.MappingRules;
    using DEHCATIA.Services.MappingConfiguration;
    using DEHCATIA.Services.ParameterTypeService;
    using DEHCATIA.ViewModels.ProductTree.Parameters;
    using DEHCATIA.ViewModels.ProductTree.Rows;
    using DEHCATIA.ViewModels.ProductTree.Shapes;

    using DEHPCommon;
    using DEHPCommon.HubController.Interfaces;

    using MECMOD;

    using Moq;

    using NUnit.Framework;

    using ProductStructureTypeLib;

    [TestFixture]
    public class CatiaProductToElementRuleTestFixture
    {
        private CatiaProductToElementRule rule;
        private Mock<IHubController> hubController;
        private Mock<IDstController> dstController;
        private Uri uri;
        private Assembler assembler;
        private DomainOfExpertise domain;
        private Mock<ISession> session;
        private Iteration iteration;
        private Mock<Product> product0;
        private Mock<Product> product1;
        private Mock<IParameterTypeService> parameterTypeService;
        private Mock<IMappingConfigurationService> mappingConfiguration;

        [SetUp]
        public void Setup()
        {
            this.uri = new Uri("https://test.test");
            this.assembler = new Assembler(this.uri);
            this.domain = new DomainOfExpertise(Guid.NewGuid(), this.assembler.Cache, this.uri);
            this.session = new Mock<ISession>();
            this.session.Setup(x => x.Assembler).Returns(this.assembler);
            this.session.Setup(x => x.DataSourceUri).Returns(this.uri.AbsoluteUri);

            this.iteration =
                new Iteration(Guid.NewGuid(), this.assembler.Cache, this.uri)
                {
                    Container = new EngineeringModel(Guid.NewGuid(), this.assembler.Cache, this.uri)
                    {
                        EngineeringModelSetup = new EngineeringModelSetup(Guid.NewGuid(), this.assembler.Cache, this.uri)
                        {
                            RequiredRdl = { new ModelReferenceDataLibrary(Guid.NewGuid(), this.assembler.Cache, this.uri) },
                            Container = new SiteReferenceDataLibrary(Guid.NewGuid(), this.assembler.Cache, this.uri)
                            {
                                Container = new SiteDirectory(Guid.NewGuid(), this.assembler.Cache, this.uri)
                            }
                        }
                    }
                };

            this.hubController = new Mock<IHubController>();
            this.hubController.Setup(x => x.CurrentDomainOfExpertise).Returns(this.domain);
            this.hubController.Setup(x => x.Session).Returns(this.session.Object);
            this.hubController.Setup(x => x.OpenIteration).Returns(this.iteration);
            this.hubController.Setup(x => x.GetSiteDirectory()).Returns(new SiteDirectory());

            this.dstController = new Mock<IDstController>();
            this.parameterTypeService = new Mock<IParameterTypeService>();
            this.parameterTypeService.Setup(x => x.Color).Returns(new TextParameterType());
            this.parameterTypeService.Setup(x => x.Material).Returns(new SampledFunctionParameterType());

            this.parameterTypeService.Setup(x => x.MomentOfInertia).Returns(new CompoundParameterType()
            {
                Name = "MoI", ShortName = "MoI"
            });

            this.mappingConfiguration = new Mock<IMappingConfigurationService>();

            var containerBuilder = new ContainerBuilder();
            containerBuilder.RegisterInstance(this.hubController.Object).As<IHubController>();
            containerBuilder.RegisterInstance(this.dstController.Object).As<IDstController>();
            containerBuilder.RegisterInstance(this.parameterTypeService.Object).As<IParameterTypeService>();
            containerBuilder.RegisterInstance(this.mappingConfiguration.Object).As<IMappingConfigurationService>();
            AppContainer.Container = containerBuilder.Build();

            this.product0 = new Mock<Product>();
            this.product0.Setup(x => x.get_DescriptionRef()).Returns(string.Empty);
            this.product0.Setup(x => x.get_PartNumber()).Returns(string.Empty);
            this.product0.Setup(x => x.get_Name()).Returns(string.Empty);
            this.product1 = new Mock<Product>();
            this.product1.Setup(x => x.get_DescriptionRef()).Returns(string.Empty);
            this.product1.Setup(x => x.get_PartNumber()).Returns(string.Empty);
            this.product1.Setup(x => x.get_Name()).Returns(string.Empty);
            this.rule = new CatiaProductToElementRule();
        }

        [Test]
        public void VerifyTransform()
        {
            var rootElement = new ElementRowViewModel(this.product0.Object, string.Empty)
            {
                Name = "RootElementRow",
                CenterOfGravity = new CenterOfGravityParameterViewModel((0, 1, 1)),
                Volume = new DoubleParameterViewModel(string.Empty,  new DoubleWithUnitValueViewModel(.2)),
                Mass = new DoubleParameterViewModel(string.Empty,  new DoubleWithUnitValueViewModel(42)),
                MomentOfInertia = new MomentOfInertiaParameterViewModel(new MassMomentOfInertiaViewModel()),
                Shape = new CatiaShapeViewModel()
                {
                    PositionOrientation = new CatiaShapePositionOrientationViewModel(
                        new[] { .1, 0, 0, 0, 1, 0, 0, 0, 1 }, new[] { .0, 0, 0 })
                }
            };
            
            var usageRowViewModel0 = new UsageRowViewModel(this.product1.Object, string.Empty)
            {
                Name = "UsageRow",
                CenterOfGravity = new CenterOfGravityParameterViewModel((0, 1, 1)),
                Volume = new DoubleParameterViewModel(string.Empty,  new DoubleWithUnitValueViewModel(.2)),
                Mass = new DoubleParameterViewModel(string.Empty,  new DoubleWithUnitValueViewModel(42)),
                MomentOfInertia = new MomentOfInertiaParameterViewModel(new MassMomentOfInertiaViewModel()),
                Shape = new CatiaShapeViewModel()
                {
                    PositionOrientation = new CatiaShapePositionOrientationViewModel(
                        new[] { .1, 0, 0, 0, 1, 0, 0, 0, 1 }, new[] { .0, 0, 0 })
                },
                ShouldMapMaterial = true,
                Parent = rootElement
            };

            var usageRowViewModel1 = new UsageRowViewModel(this.product1.Object, string.Empty)
            {
                Name = "UsageRow",
                CenterOfGravity = new CenterOfGravityParameterViewModel((0, 1, 1)),
                Volume = new DoubleParameterViewModel(string.Empty,  new DoubleWithUnitValueViewModel(.2)),
                Mass = new DoubleParameterViewModel(string.Empty,  new DoubleWithUnitValueViewModel(42)),
                MomentOfInertia = new MomentOfInertiaParameterViewModel(new MassMomentOfInertiaViewModel()),
                Shape = new CatiaShapeViewModel()
                {
                    PositionOrientation = new CatiaShapePositionOrientationViewModel(
                        new[] { .1, 0, 0, 0, 1, 0, 0, 0, 1 }, new[] { .0, 0, 0 })
                },
                ShouldMapMaterial = true,
                Parent = rootElement
            };

            var definitionRowViewModel0 = new DefinitionRowViewModel(this.product1.Object, string.Empty)
            {
                Name = "DefinitionRow",
                CenterOfGravity = new CenterOfGravityParameterViewModel((0, 1, 1)),
                Volume = new DoubleParameterViewModel(string.Empty,  new DoubleWithUnitValueViewModel(.2)),
                Mass = new DoubleParameterViewModel(string.Empty,  new DoubleWithUnitValueViewModel(42)),
                MomentOfInertia = new MomentOfInertiaParameterViewModel(new MassMomentOfInertiaViewModel()),
                Shape = new CatiaShapeViewModel()
                {
                    PositionOrientation = new CatiaShapePositionOrientationViewModel(
                        new[] { .1, 0, 0, 0, 1, 0, 0, 0, 1 }, new[] { .0, 0, 0 })
                },
                Parent = usageRowViewModel0,
                ShouldMapMaterial = false
            };

            var body = new Mock<Body>();
            body.Setup(x => x.get_Name()).Returns("body.0");

            var definitionRowViewModel1 = new DefinitionRowViewModel(this.product1.Object, string.Empty)
            {
                Name = "DefinitionRow",
                CenterOfGravity = new CenterOfGravityParameterViewModel((0, 1, 1)),
                Volume = new DoubleParameterViewModel(string.Empty, new DoubleWithUnitValueViewModel(.2)),
                Mass = new DoubleParameterViewModel(string.Empty, new DoubleWithUnitValueViewModel(42)),
                MomentOfInertia = new MomentOfInertiaParameterViewModel(new MassMomentOfInertiaViewModel()),
                Shape = new CatiaShapeViewModel()
                {
                    PositionOrientation = new CatiaShapePositionOrientationViewModel(
                        new[] { .1, 0, 0, 0, 1, 0, 0, 0, 1 }, new[] { .0, 0, 0 })
                },
                Parent = usageRowViewModel1,
                Children = { new BodyRowViewModel(body.Object, "") }
            };

            usageRowViewModel0.Children.Add(definitionRowViewModel0);
            usageRowViewModel1.Children.Add(definitionRowViewModel1);
            rootElement.Children.Add(usageRowViewModel0);
            rootElement.Children.Add(usageRowViewModel1);
            
            var result = new List<(ElementRowViewModel Parent, ElementBase Element)>();

            Assert.DoesNotThrow(() => result = this.rule.Transform(rootElement));
            Assert.IsNotNull(result);
            Assert.AreEqual(4, result.Count);
        }

        [Test]
        public void VerifyMapShape()
        {
            var rootElement = new ElementRowViewModel(this.product0.Object, string.Empty)
            {
                Name = "RootElementRow",
                CenterOfGravity = new CenterOfGravityParameterViewModel((0, 1, 1)),
                Volume = new DoubleParameterViewModel(string.Empty,  new DoubleWithUnitValueViewModel(.2)),
                Mass = new DoubleParameterViewModel(string.Empty,  new DoubleWithUnitValueViewModel(42)),
                MomentOfInertia = new MomentOfInertiaParameterViewModel(new MassMomentOfInertiaViewModel()),
                Shape = new CatiaShapeViewModel()
                {
                    PositionOrientation = new CatiaShapePositionOrientationViewModel(
                           new[] { .1, 0, 0, 0, 1, 0, 0, 0, 1 }, new[] { .0, 0, 0 })
                }
            };

            var usageRowViewModel = new UsageRowViewModel(this.product1.Object, string.Empty)
            {
                Name = "UsageRow",
                CenterOfGravity = new CenterOfGravityParameterViewModel((0, 1, 1)),
                Volume = new DoubleParameterViewModel(string.Empty,  new DoubleWithUnitValueViewModel(.2)),
                Mass = new DoubleParameterViewModel(string.Empty,  new DoubleWithUnitValueViewModel(42)),
                MomentOfInertia = new MomentOfInertiaParameterViewModel(new MassMomentOfInertiaViewModel()),
                Shape = new CatiaShapeViewModel()
                {
                    PositionOrientation = new CatiaShapePositionOrientationViewModel(
                        new[] { .1, 0, 0, 0, 1, 0, 0, 0, 1 }, new[] { .0, 0, 0 }),
                    ShapeKind = new ShapeKindParameterViewModel(ShapeKind.Cone),
                    Length = new DoubleParameterViewModel(string.Empty,  new DoubleWithUnitValueViewModel(2)),
                    WidthOrDiameter = new DoubleParameterViewModel(string.Empty,  new DoubleWithUnitValueViewModel(42)),
                    Height = new DoubleParameterViewModel(string.Empty,  new DoubleWithUnitValueViewModel(2)),
                    LengthSupport = new DoubleParameterViewModel(string.Empty,  new DoubleWithUnitValueViewModel(2)),
                    Angle = new DoubleParameterViewModel(string.Empty,  new DoubleWithUnitValueViewModel(6)),
                    AngleSupport = new DoubleParameterViewModel(string.Empty,  new DoubleWithUnitValueViewModel(453)),
                    Thickness = new DoubleParameterViewModel(string.Empty,  new DoubleWithUnitValueViewModel(54)),
                    IsSupported = true,
                },
                Parent = rootElement
            };

            var definitionRowViewModel = new DefinitionRowViewModel(this.product1.Object, string.Empty)
            {
                Name = "DefinitionRow",
                CenterOfGravity = new CenterOfGravityParameterViewModel((0, 1, 1)),
                Volume = new DoubleParameterViewModel(string.Empty,  new DoubleWithUnitValueViewModel(.2)),
                Mass = new DoubleParameterViewModel(string.Empty,  new DoubleWithUnitValueViewModel(42)),
                MomentOfInertia = new MomentOfInertiaParameterViewModel(new MassMomentOfInertiaViewModel()),
                Shape = new CatiaShapeViewModel()
                {
                    PositionOrientation = new CatiaShapePositionOrientationViewModel(
                        new[] { .1, 0, 0, 0, 1, 0, 0, 0, 1 }, new[] { .0, 0, 0 }),
                    ShapeKind = new ShapeKindParameterViewModel(ShapeKind.Paraboloid),
                    Length = new DoubleParameterViewModel(string.Empty,  new DoubleWithUnitValueViewModel(2)),
                    WidthOrDiameter = new DoubleParameterViewModel(string.Empty,  new DoubleWithUnitValueViewModel(42)),
                    Height = new DoubleParameterViewModel(string.Empty,  new DoubleWithUnitValueViewModel(2)),
                    LengthSupport =  new DoubleParameterViewModel(string.Empty,  new DoubleWithUnitValueViewModel(2)),
                    Angle = new DoubleParameterViewModel(string.Empty,  new DoubleWithUnitValueViewModel(6)),
                    AngleSupport = new DoubleParameterViewModel(string.Empty,  new DoubleWithUnitValueViewModel(453)),
                    Thickness = new DoubleParameterViewModel(string.Empty,  new DoubleWithUnitValueViewModel(54)),
                    IsSupported = true,
                },
                Parent = usageRowViewModel
            };

            usageRowViewModel.Children.Add(definitionRowViewModel);
            rootElement.Children.Add(usageRowViewModel);

            var result = new List<(ElementRowViewModel Parent, ElementBase Element)>();

            Assert.DoesNotThrow(() => result = this.rule.Transform(rootElement));
            Assert.IsNotNull(result);
            Assert.AreEqual(3, result.Count);

            Assert.AreEqual(2, result
                .Select(x => x.Element)
                .OfType<ElementDefinition>()
                .FirstOrDefault()?
                .Parameter.Count);

            Assert.AreEqual(2, result
                .Select(x => x.Element)
                .OfType<ElementDefinition>()
                .FirstOrDefault(x => x.ShortName == "DefinitionRow")?
                .Parameter.Count);
        }
    }
}
