// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;

namespace CP.IO.Ports;

/// <summary>
/// PendingRequest.
/// </summary>
public record PendingRequest(string Command, Action<string> Apply, TaskCompletionSource<bool> Completion);
