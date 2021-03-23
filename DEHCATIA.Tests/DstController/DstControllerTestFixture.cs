﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="DstControllerTestFixture.cs" company="RHEA System S.A.">
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

namespace DEHCATIA.Tests.DstController
{
    using System.Reactive.Concurrency;

    using DEHCATIA.DstController;
    using DEHCATIA.Services.ComConnector;
    using DEHCATIA.ViewModels.ProductTree.Rows;

    using DEHPCommon.UserInterfaces.ViewModels.Interfaces;

    using Moq;

    using NUnit.Framework;

    using ReactiveUI;

    [TestFixture]
    public class DstControllerTestFixture
    {
        private DstController controller;
        private Mock<ICatiaComService> comService;
        private Mock<IStatusBarControlViewModel> statusBar;

        [SetUp]
        public void Setup()
        {
            RxApp.MainThreadScheduler = Scheduler.CurrentThread;
            this.comService = new Mock<ICatiaComService>();
            this.statusBar = new Mock<IStatusBarControlViewModel>();
            this.controller = new DstController(this.comService.Object, this.statusBar.Object);
        }
        
        [Test]
        public void VerifyConnectDisconnectToCatia()
        {
            Assert.IsFalse(this.controller.IsCatiaConnected);   

            Assert.DoesNotThrow(() => this.controller.ConnectToCatia());
            Assert.DoesNotThrow(() => this.controller.DisconnectFromCatia());
            this.comService.Verify(x => x.Connect(), Times.Once);
            this.comService.Verify(x => x.Disconnect(), Times.Once);
        }

        [Test]
        public void VerifyGetProductTree()
        {
            Assert.IsFalse(this.controller.IsCatiaConnected);
            this.comService.Setup(x => x.GetProductTree()).Returns(default(ElementRowViewModel));
            Assert.IsNull(this.controller.GetProductTree());
            this.comService.Verify(x => x.GetProductTree(), Times.Once);
        }

        [Test]
        public void VerifyIsConnected()
        {
            Assert.IsFalse(this.controller.IsCatiaConnected);
            this.comService.Setup(x => x.IsCatiaConnected).Returns(true);
            this.controller = new DstController(this.comService.Object, this.statusBar.Object);
            Assert.IsTrue(this.controller.IsCatiaConnected);
        }
    }
}