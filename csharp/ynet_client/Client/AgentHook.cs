// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT

using System.Diagnostics;

namespace Ynet.Client;

/// <summary>What happened when the client tried to notify the agent.</summary>
public enum HookOutcome
{
    /// <summary>The hook ran and reported success. The alert still stays pending until drained.</summary>
    Delivered,

    /// <summary>No hook is configured. The alert is durable and waits to be drained.</summary>
    NotConfigured,

    /// <summary>The hook was attempted and failed. Recorded; the alert stays pending.</summary>
    Failed,
}

/// <summary>The result of one notification attempt, including why it did not reach the agent.</summary>
public sealed record HookAttempt(HookOutcome Outcome, string AlertId, string? Detail = null);

/// <summary>
/// M6-e and M6-f: the callback out of the receiver into the agent.
///
/// Two rules the shape of this class exists to enforce:
///
/// 1. Notification NEVER blocks receipt. The hook is invoked with a hard timeout and its failure
///    is a recorded value, not an exception thrown into the state machine. A dead agent slows
///    nothing down and loses nothing.
///
/// 2. The hook is an ANNOUNCEMENT, not a handover. It does not carry the work and it does not
///    delete the alert. The durable record in the spool is what the agent acts on, whenever the
///    agent decides to — which is precisely the "/btw" semantics: the agent chooses between
///    interrupting what it is doing and picking the alert up later, and neither choice can lose it.
/// </summary>
public sealed class AgentHook
{
    private readonly string? _command;
    private readonly TimeSpan _timeout;

    /// <param name="command">
    /// Optional command line invoked as: &lt;command&gt; &lt;alertId&gt; &lt;messageId&gt; &lt;origin&gt;.
    /// When null or empty, the client is notification-durable but hook-silent, which is a valid
    /// configuration and is reported as <see cref="HookOutcome.NotConfigured"/> rather than success.
    /// </param>
    /// <param name="timeout">Hard bound on the hook. Exceeding it is a failure, never a hang.</param>
    public AgentHook(string? command, TimeSpan? timeout = null)
    {
        _command = string.IsNullOrWhiteSpace(command) ? null : command.Trim();
        _timeout = timeout ?? TimeSpan.FromSeconds(5);
    }

    /// <summary>True when a hook command is configured.</summary>
    public bool IsConfigured => _command is not null;

    /// <summary>Announce one alert to the agent. Always returns; never throws.</summary>
    public HookAttempt Notify(PendingAlert alert)
    {
        ArgumentNullException.ThrowIfNull(alert);
        if (_command is null) return new HookAttempt(HookOutcome.NotConfigured, alert.AlertId);

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = _command,
                UseShellExecute = false,
                // NOT redirected (codex cycle 1, P2). A redirected pipe nobody reads fills, the
                // child blocks on its own write, and a perfectly healthy hook is then killed and
                // reported as a timeout. The hook's output is not used for anything, so the honest
                // fix is to stop capturing it rather than to add a reader for output we discard.
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add(alert.AlertId);
            psi.ArgumentList.Add(alert.MessageId);
            psi.ArgumentList.Add(alert.Origin);

            using var p = Process.Start(psi);
            if (p is null) return new HookAttempt(HookOutcome.Failed, alert.AlertId, "process did not start");

            if (!p.WaitForExit(_timeout))
            {
                try { p.Kill(entireProcessTree: true); } catch { /* already gone */ }
                return new HookAttempt(HookOutcome.Failed, alert.AlertId, $"hook exceeded {_timeout.TotalSeconds:0.#}s");
            }

            return p.ExitCode == 0
                ? new HookAttempt(HookOutcome.Delivered, alert.AlertId)
                : new HookAttempt(HookOutcome.Failed, alert.AlertId, $"hook exit {p.ExitCode}");
        }
        catch (Exception ex)
        {
            return new HookAttempt(HookOutcome.Failed, alert.AlertId, ex.GetType().Name + ": " + ex.Message);
        }
    }
}
