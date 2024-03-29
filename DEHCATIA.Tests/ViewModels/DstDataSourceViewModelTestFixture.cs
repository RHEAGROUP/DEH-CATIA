﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="DstDataSourceViewModelTestFixture.cs" company="RHEA System S.A.">
//    Copyright (c) 2021 RHEA System S.A.
//
//    Author: Sam Gerené, Alex Vorobiev, Alexander van Delft, Nathanael Smiechowski, Ahmed Abulwafa Ahmed
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

namespace DEHCATIA.Tests.ViewModels
{
    using System.Reactive.Concurrency;

    using Autofac;

    using CDP4Common.EngineeringModelData;

    using DEHCATIA.DstController;
    using DEHCATIA.ViewModels;
    using DEHCATIA.ViewModels.Dialogs.Interfaces;
    using DEHCATIA.ViewModels.Interfaces;
    using DEHCATIA.Views.Dialogs;

    using DEHPCommon;
    using DEHPCommon.HubController.Interfaces;
    using DEHPCommon.Services.NavigationService;
    using DEHPCommon.UserInterfaces.ViewModels.Interfaces;

    using Moq;

    using NUnit.Framework;

    using ReactiveUI;

    [TestFixture]
    public class DstDataSourceViewModelTestFixture
    {
        private Mock<IDstController> dstController;
        private Mock<INavigationService> navigationService;
        private Mock<IDstBrowserHeaderViewModel> dstBrowserHeader;
        private Mock<IDstProductTreeViewModel> dstProductTree;

        private DstDataSourceViewModel viewModel;
        private Mock<IStatusBarControlViewModel> statusBar;
        private Mock<IHubController> hubController;
        private Mock<IDstLoginViewModel> loginViewModel;

        [SetUp]
        public void Setup()
        {
            RxApp.MainThreadScheduler = Scheduler.CurrentThread;

            this.dstController = new Mock<IDstController>();
            this.navigationService = new Mock<INavigationService>();
            this.dstBrowserHeader = new Mock<IDstBrowserHeaderViewModel>();
            this.dstProductTree = new Mock<IDstProductTreeViewModel>();
            this.dstProductTree = new Mock<IDstProductTreeViewModel>();
            this.statusBar = new Mock<IStatusBarControlViewModel>();
            this.hubController = new Mock<IHubController>();
            this.loginViewModel = new Mock<IDstLoginViewModel>();

            var containerBuilder = new ContainerBuilder();
            containerBuilder.RegisterInstance(this.loginViewModel.Object).As<IDstLoginViewModel>();
            AppContainer.Container = containerBuilder.Build();

            this.viewModel = new DstDataSourceViewModel(this.navigationService.Object, this.dstController.Object, this.dstBrowserHeader.Object, 
                this.dstProductTree.Object, this.statusBar.Object, this.hubController.Object);
        }

        [Test]
        public void VerifyProperties()
        {
            Assert.IsNotNull(this.viewModel.ConnectCommand);
            Assert.AreEqual("Connect", this.viewModel.ConnectButtonText);
            Assert.AreEqual("Connection is not established", this.viewModel.ConnectionStatus);
            Assert.IsFalse(this.viewModel.ConnectCommand.CanExecute(null));
            this.hubController.Setup(x => x.OpenIteration).Returns(new Iteration());

            this.viewModel = new DstDataSourceViewModel(this.navigationService.Object, this.dstController.Object, this.dstBrowserHeader.Object,
                this.dstProductTree.Object, this.statusBar.Object, this.hubController.Object);

            Assert.IsTrue(this.viewModel.ConnectCommand.CanExecute(null));
        }

        [Test]
        public void VerifyConnectCommand()
        {
            RxApp.MainThreadScheduler = Scheduler.CurrentThread;

            this.dstController.Setup(x => x.IsCatiaConnected).Returns(false);
            this.viewModel.ConnectCommand.Execute(null);

            Assert.AreEqual("Connection is not established", this.viewModel.ConnectionStatus);

            this.navigationService.Setup(x =>
                    x.ShowDxDialog<DstLogin, IDstLoginViewModel>(It.IsAny<IDstLoginViewModel>()))
                .Returns(true);

            this.dstController.Setup(x => x.ConnectToCatia())
                .Callback(() => this.dstController.Setup(c => c.IsCatiaConnected).Returns(true));
            
            this.viewModel.ConnectCommand.Execute(null);
            Assert.AreEqual("Connection is established", this.viewModel.ConnectionStatus);

            this.dstController.Setup(x => x.IsCatiaConnected).Returns(false);
            this.viewModel.ConnectCommand.Execute(null);
            Assert.AreEqual("Connection is established", this.viewModel.ConnectionStatus);

            this.dstController.Verify(x => x.ConnectToCatia(), Times.Exactly(2));

            this.dstController.Setup(x => x.IsCatiaConnected).Returns(true);
           
            this.dstController.Setup(x => x.DisconnectFromCatia())
                .Callback(() => this.dstController.Setup(c => c.IsCatiaConnected).Returns(false));

            this.viewModel.ConnectCommand.Execute(null);

            Assert.AreEqual("Connection is not established", this.viewModel.ConnectionStatus);

            this.dstController.Verify(x => x.DisconnectFromCatia(), Times.Once);
        }
    }
}
