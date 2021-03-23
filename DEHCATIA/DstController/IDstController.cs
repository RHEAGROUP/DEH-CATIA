﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IDstController.cs" company="RHEA System S.A.">
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

namespace DEHCATIA.DstController
{
    using DEHCATIA.ViewModels.ProductTree.Rows;

    /// <summary>
    /// Interface definition for <see cref="DstController"/>
    /// </summary>
    public interface IDstController
    {
        /// <summary>
        /// Gets or sets whether there's a connection to a running CATIA client.
        /// </summary>
        bool IsCatiaConnected { get; set; }

        /// <summary>
        /// Retrieves the product tree
        /// </summary>
        /// <returns>The root <see cref="ElementRowViewModel"/></returns>
        ElementRowViewModel GetProductTree();

        /// <summary>
        /// Connects to the Catia running instance
        /// </summary>
        void ConnectToCatia();

        /// <summary>
        /// Disconnect from the Catia running instance
        /// </summary>
        void DisconnectFromCatia();
    }
}