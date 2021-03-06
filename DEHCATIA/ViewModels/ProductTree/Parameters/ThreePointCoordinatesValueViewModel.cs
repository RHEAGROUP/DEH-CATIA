﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ThreePointCoordinatesValueViewModel.cs" company="RHEA System S.A.">
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

namespace DEHCATIA.ViewModels.ProductTree.Parameters
{
    using System.Collections.Generic;

    using ReactiveUI;

    /// <summary>
    /// The <see cref="ThreePointCoordinatesValueViewModel"/> represents a value that holds 3 values expressable as X,Y,Z
    /// </summary>
    public abstract class ThreePointCoordinatesValueViewModel : DstParameterViewModel<(double X, double Y, double Z)>
    {
        /// <summary>
        /// Initializes a new <see cref="DstParameterViewModel{TValueType}"/>
        /// </summary>
        /// <param name="value">The value</param>
        protected ThreePointCoordinatesValueViewModel((double X, double Y, double Z) value) : base("CenterOfGravity", value)
        {
            this.Name = "CenterOfGravity";
        }

        /// <summary>
        /// Gets a collection reprentation of the <see cref="DstParameterViewModel{TValueType}.Value"/> for display purpose
        /// </summary>
        public ReactiveList<object> AsRow => new ReactiveList<object>(
            new List<object>()
            {
                new {X = $"{this.Value.X} mm", Y = $"{this.Value.Y} mm", Z = $"{this.Value.Z} mm"}
            });

        /// <summary>
        /// Gets the value as an array of double
        /// </summary>
        public double[] Values => new[] { this.Value.X, this.Value.Y, this.Value.Z };

        /// <summary>Returns a string that represents the current object.</summary>
        /// <returns>A string that represents the current object.</returns>
        public override string ToString()
        {
            return $"X {this.Value.X} | Y {this.Value.Y} | Z {this.Value.Z}";
        }
    }
}
