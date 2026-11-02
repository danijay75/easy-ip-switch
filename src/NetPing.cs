using System;
using System.Diagnostics;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;

namespace EasyIpSwitch
{
    internal sealed class PingOutcome
    {
        public bool Success;
        public long Milliseconds;
        public string Detail = "";
        public string Source = "";
        public string Target = "";
    }

    internal static class NetPing
    {
        private static readonly Regex TimePattern = new Regex(@"([0-9]+)\s*ms", RegexOptions.IgnoreCase);

        public static PingOutcome Send(string sourceIp, string target, int timeoutMs)
        {
            PingOutcome outcome = new PingOutcome();
            outcome.Source = sourceIp ?? "";
            outcome.Target = target ?? "";

            if (string.IsNullOrEmpty(target))
            {
                outcome.Detail = L.T("Adresse vide.");
                return outcome;
            }

            if (!string.IsNullOrEmpty(sourceIp))
            {
                return SendViaPingExe(sourceIp, target, timeoutMs);
            }

            return SendDirect(target, timeoutMs);
        }

        private static PingOutcome SendDirect(string target, int timeoutMs)
        {
            PingOutcome outcome = new PingOutcome();
            outcome.Source = "";
            outcome.Target = target;

            try
            {
                using (Ping ping = new Ping())
                {
                    PingReply reply = ping.Send(target, timeoutMs);
                    outcome.Success = reply.Status == IPStatus.Success;
                    outcome.Milliseconds = reply.RoundtripTime;
                    if (outcome.Success)
                    {
                        outcome.Detail = L.F("Réponse en {0} ms", reply.RoundtripTime);
                    }
                    else
                    {
                        outcome.Detail = StatusText(reply.Status);
                    }
                }
            }
            catch (Exception ex)
            {
                outcome.Detail = ex.Message;
            }
            return outcome;
        }

        private static PingOutcome SendViaPingExe(string sourceIp, string target, int timeoutMs)
        {
            PingOutcome outcome = new PingOutcome();
            outcome.Source = sourceIp;
            outcome.Target = target;

            Stopwatch watch = Stopwatch.StartNew();
            try
            {
                ProcessStartInfo start = new ProcessStartInfo();
                start.FileName = "ping.exe";
                start.Arguments = "-n 1 -w " + timeoutMs.ToString(CultureInfo.InvariantCulture)
                    + " -S " + sourceIp + " " + target;
                start.UseShellExecute = false;
                start.CreateNoWindow = true;
                start.RedirectStandardOutput = true;
                start.RedirectStandardError = true;

                using (Process process = Process.Start(start))
                {
                    if (process == null)
                    {
                        return SendDirect(target, timeoutMs);
                    }

                    string output = process.StandardOutput.ReadToEnd();
                    if (!process.WaitForExit(timeoutMs + 2000))
                    {
                        try
                        {
                            process.Kill();
                        }
                        catch
                        {
                        }
                        outcome.Detail = L.T("Délai dépassé");
                        return outcome;
                    }

                    watch.Stop();
                    outcome.Success = process.ExitCode == 0;
                    outcome.Milliseconds = watch.ElapsedMilliseconds;

                    Match match = TimePattern.Match(output ?? "");
                    if (match.Success)
                    {
                        long value;
                        if (long.TryParse(match.Groups[1].Value, out value))
                        {
                            outcome.Milliseconds = value;
                        }
                    }

                    if (outcome.Success)
                    {
                        outcome.Detail = L.F("Réponse en {0} ms", outcome.Milliseconds);
                    }
                    else
                    {
                        outcome.Detail = DescribeFailure(output);
                    }
                }
            }
            catch (Exception ex)
            {
                outcome.Detail = ex.Message;
            }
            return outcome;
        }

        private static string DescribeFailure(string output)
        {
            if (string.IsNullOrEmpty(output))
            {
                return L.T("Aucune réponse");
            }
            string lower = output.ToLowerInvariant();
            if (lower.Contains("délai") || lower.Contains("timed out") || lower.Contains("timeout"))
            {
                return L.T("Délai dépassé");
            }
            if (lower.Contains("inaccessible") || lower.Contains("unreachable"))
            {
                return L.T("Hôte inaccessible");
            }
            if (lower.Contains("introuvable") || lower.Contains("could not find")
                || lower.Contains("nom d'hôte") || lower.Contains("host"))
            {
                return L.T("Hôte introuvable");
            }
            return L.T("Aucune réponse");
        }

        private static string StatusText(IPStatus status)
        {
            if (status == IPStatus.TimedOut)
            {
                return L.T("Délai dépassé");
            }
            if (status == IPStatus.DestinationHostUnreachable)
            {
                return L.T("Hôte inaccessible");
            }
            if (status == IPStatus.DestinationNetworkUnreachable)
            {
                return L.T("Réseau inaccessible");
            }
            if (status == IPStatus.Success)
            {
                return L.T("Réponse");
            }
            return status.ToString();
        }
    }
}
