// <copyright file="IDisposableExtensions.cs" company="Chris Pulman">
// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Collections.Generic;

namespace CP.IO.Ports;

/// <summary>
/// IDisposable Extensions.
/// </summary>
public static class IDisposableExtensions
{
    /// <summary>
    /// Add disposable(self) to CompositeDisposable(or other ICollection).
    /// </summary>
    /// <typeparam name="T">The type.</typeparam>
    /// <param name="disposable">The disposable.</param>
    /// <param name="container">The container.</param>
    /// <returns>Type of T.</returns>
    public static T AddTo<T>(this T disposable, ICollection<IDisposable> container)
        where T : IDisposable
    {
        container.Add(disposable);
        return disposable;
    }
}
