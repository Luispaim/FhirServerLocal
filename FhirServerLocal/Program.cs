using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using System.Web;
using System.Net.Http;
using Hl7.Fhir.Rest;

namespace smart_local
{
    /// <summary>
    /// Main program
    /// </summary>
    public static class Program
    {
        private const string _clientId = "fhir_demo_id";
        private const string _defaultFhirServerUrl = "https://launch.smarthealthit.org/v/r4/sim/eyJoIjoiMSIsImUiOiJlNDQzYWM1OC04ZWNlLTQzODUtOGQ1NS03NzVjMWI4ZjNhMzcifQ/fhir/";

        private static string _authCode = string.Empty;
        private static string _clientState = string.Empty;

        private static string _redirectUrl = string.Empty;

        private static string _tokenUrl = string.Empty;

        /// <summary>
        /// Program to access a SMART FHIR Server with a local webserver for redirection
        /// </summary>
        /// <param name="fhirServerUrl">FHIR R4 endpoint URL</param>
        /// <returns></returns>
        static int Main(string[] fhirServerUrl)
        {
            fhirServerUrl = new string[1];
            if (string.IsNullOrEmpty(fhirServerUrl[0]))
            {
                fhirServerUrl[0] = _defaultFhirServerUrl;
            }

            Console.WriteLine($"FHIR Server: {fhirServerUrl[0]}");

            FhirClient fhirClient = new FhirClient(fhirServerUrl[0]);

            if (!FhirUtils.TryGetSmartUrls(fhirClient, out string authorizeUrl, out string tokenUrl))
            {
                System.Console.WriteLine($"Failed to discover SMART URLs");
                return -1;
            }

            System.Console.WriteLine($"Authorize URL: {authorizeUrl}");
            System.Console.WriteLine($"    Token URL: {tokenUrl}");
            _tokenUrl = tokenUrl;

            Task.Run(() => CreateHostBuilder().Build().Run());

            int listenPort = GetListenPort().Result;

            System.Console.WriteLine($" Listening on: {listenPort}");
            _redirectUrl = $"http://127.0.0.1:{listenPort}";

            // https://ehr/authorize?
            // response_type=code&
            // client_id=app-client-id&
            // redirect_uri=https%3A%2F%2Fapp%2Fafter-auth&
            // launch=xyz123&
            // scope=launch+patient%2FObservation.read+patient%2FPatient.read+openid+fhirUser&
            // state=98wrghuwuogerg97&
            // aud=https://ehr/fhir

            string url =
                $"{authorizeUrl}" +
                $"?response_type=code" +
                $"&client_id={_clientId}" +
                $"&redirect_uri={HttpUtility.UrlEncode(_redirectUrl)}" +
                $"&scope={HttpUtility.UrlEncode("openid fhirUser profile launch/patient patient/*.read")}" +
                $"&state=local_state" +
                $"&aud={fhirServerUrl[0]}";

            LaunchUrl(url);

            // http://127.0.0.1:54678/?code=eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJjb250ZXh0Ijp7Im5lZWRfcGF0aWVudF9iYW5uZXIiOnRydWUsInNtYXJ0X3N0eWxlX3VybCI6Imh0dHBzOi8vbGF1bmNoLnNtYXJ0aGVhbHRoaXQub3JnL3NtYXJ0LXN0eWxlLmpzb24iLCJwYXRpZW50IjoiMmNkYTVhYWQtZTQwOS00MDcwLTlhMTUtZTFjMzVjNDZlZDVhIn0sImNsaWVudF9pZCI6ImZoaXJfZGVtb19pZCIsInNjb3BlIjoib3BlbmlkIGZoaXJVc2VyIHByb2ZpbGUgbGF1bmNoL3BhdGllbnQgcGF0aWVudC8qLnJlYWQiLCJ1c2VyIjoiUHJhY3RpdGlvbmVyL2VmYjVkNGNlLWRmZmMtNDdkZi1hYTZkLTA1ZDM3MmZkYjQwNyIsImlhdCI6MTYwNDMzMjkxNywiZXhwIjoxNjA0MzMzMjE3fQ.RkZEP3eKVydMLc5wW0FJEtTqYoTOuwgtoTcjhjevAC0&state=local_state

            for (int loops = 0; loops < 30; loops++)
            {
                System.Threading.Thread.Sleep(1000);
            }

            return 0;
        }

        /// <summary>
        /// Set the authorization code and state
        /// </summary>
        /// <param name="code"></param>
        /// <param name="state"></param>
        public static async void SetAuthCode(string code, string state)
        {
            _authCode = code;
            _clientState = state;

            System.Console.WriteLine($"Code received: {code}");

            Dictionary<string, string> requestValues = new Dictionary<string, string>()
            {
                { "grant_type", "authorization_code" },
                { "code", code },
                { "redirect_uri", _redirectUrl },
                { "client_id", _clientId },
            };

            HttpRequestMessage request = new HttpRequestMessage()
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(_tokenUrl),
                Content = new FormUrlEncodedContent(requestValues),
            };

            HttpClient client = new HttpClient();

            HttpResponseMessage response = await client.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                System.Console.WriteLine($"Failed to exchange code for token!");
                throw new Exception($"Unauthorized: {response.StatusCode}");
            }

            string json = await response.Content.ReadAsStringAsync();

            System.Console.WriteLine($"----- Token -----");
            System.Console.WriteLine(json);
            System.Console.WriteLine($"----- Token -----");
        }

        /// <summary>
        /// Launch a URL in the user's default web browser.
        /// </summary>
        /// <param name="url"></param>
        /// <returns>true if successful, false otherwise</returns>
        public static bool LaunchUrl(string url)
        {
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo()
                {
                    FileName = url,
                    UseShellExecute = true,
                };

                Process.Start(startInfo);
                return true;
            }
            catch (Exception)
            {
                // ignore
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    url = url.Replace("&", "^&");
                    Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
                    return true;
                }
                catch (Exception)
                {
                    // ignore
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                string[] allowedProgramsToRun = { "xdg-open", "gnome-open", "kfmclient" };

                foreach (string helper in allowedProgramsToRun)
                {
                    try
                    {
                        Process.Start(helper, url);
                        return true;
                    }
                    catch (Exception)
                    {
                        // ignore
                    }
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                try
                {
                    Process.Start("open", url);
                    return true;
                }
                catch (Exception)
                {
                    // ignore
                }
            }

            System.Console.WriteLine($"Failed to launch URL");
            return false;
        }

        /// <summary>
        /// Determine the listening port of the web server
        /// </summary>
        /// <returns></returns>
        public static async Task<int> GetListenPort()
        {
            for (int loops = 0; loops < 100; loops++)
            {
                await Task.Delay(100);
                if (Startup.Addresseses == null)
                {
                    continue;
                }

                string address = Startup.Addresseses.Addresses.FirstOrDefault();

                if (string.IsNullOrEmpty(address))
                {
                    continue;
                }

                if (address.Length < 18)
                {
                    continue;
                }

                if ((int.TryParse(address.Substring(17), out int port)) &&
                    (port != 0))
                {
                    return port;
                }
            }

            throw new Exception($"Failed to get listen port!");
        }

        public static IHostBuilder CreateHostBuilder() =>
            Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseUrls("http://127.0.0.1:0");
                    webBuilder.UseKestrel();
                    webBuilder.UseStartup<Startup>();
                });
    }
}