// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace CP.IO.Ports;

/// <summary>Represents a pending command request awaiting a serial response.</summary>
/// <param name="Command">The command text sent to the serial port.</param>
/// <param name="Apply">The action that applies the response payload.</param>
/// <param name="Completion">The completion source signaled when a response arrives.</param>
public sealed record PendingRequest(string Command, Action<string> Apply, TaskCompletionSource<bool> Completion);
