using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace ReplitDbClient {
    public sealed class ReplitDBClient : IReplitDbClient, IDisposable {
        private readonly String url;
        private readonly HttpClient http;

        public ReplitDBClient(String url) {
            this.url = url ?? throw new ArgumentNullException(nameof(url));
            this.http = new HttpClient();
        }

        public async Task Set(String key, String value) {
            using var response =
                await this.http.PostAsync(this.url, new FormUrlEncodedContent(new Dictionary<String, String> {
                    { key, value }
                })).ConfigureAwait(false);

            CheckResponse(response);
        }

        public async Task<String> Get(String key) {
            using var response = await this.http.GetAsync(this.GetRequestUri(key)).ConfigureAwait(false);

            CheckResponse(response);

            // Get the response content.
            if (response.Content != null) {
                return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            } else {
                // No content to return.
                return String.Empty;
            }
        }

        public async Task Delete(String key) {
            using var response =
                await this.http.DeleteAsync(this.GetRequestUri(key)).ConfigureAwait(false);

            CheckResponse(response);
        }

        public Task<IEnumerable<String>> ListKeys(String prefix = "") {
            return Task.FromResult<IEnumerable<String>>(new KeyListEnumerable(this.url, prefix));
        }

        public void Dispose() {
            this.http.Dispose();
        }

        private String GetRequestUri(String key) {
            return $"{this.url}/{Uri.EscapeDataString(key)}";
        }

        private static void CheckResponse(HttpResponseMessage response) {
            switch (response.StatusCode) {
                case HttpStatusCode.Accepted:
                case HttpStatusCode.Created:
                case HttpStatusCode.OK:
                    // Success
                    return;
                case HttpStatusCode.Forbidden:
                    throw new UnauthorizedAccessException("Access denied.");
                case HttpStatusCode.Unauthorized:
                    throw new UnauthorizedAccessException("Unauthenticated access denied.");
                case HttpStatusCode.NotFound:
                    throw new InvalidOperationException("The specified URL was not found.");
                case HttpStatusCode.InternalServerError:
                    throw new ServiceException(
                        "The ReplitDB service encountered an internal error.",
                        response.StatusCode
                    );
                case HttpStatusCode.ServiceUnavailable:
                case HttpStatusCode.BadGateway:
                case HttpStatusCode.GatewayTimeout:
                    throw new ServiceException(
                        "The ReplitDB service is not currently reachable.",
                        response.StatusCode
                    );
                case HttpStatusCode.BadRequest:
                    throw new InvalidOperationException(
                        "The ReplitDB service reported the request as invalid."
                    );
                default:
                    throw new NotSupportedException(
                        $"Unexpected HTTP Status Code: {response.StatusCode}"
                    );
            }
        }

        private class KeyListEnumerable : IEnumerable<String> {
            private readonly String url;
            private readonly String prefix;

            public KeyListEnumerable(String url, String prefix) {
                this.url = url;
                this.prefix = prefix;
            }

            public IEnumerator<String> GetEnumerator() {
                var http = new HttpClient();

                // Need to do this synchronously
                var response =
                    http.GetAsync($"{this.url}?encode=true&prefix={Uri.EscapeDataString(this.prefix)}")
                        .ConfigureAwait(false)
                        .GetAwaiter()
                        .GetResult();

                CheckResponse(response);

                return new KeyListEnumerator(http, response);
            }

            IEnumerator IEnumerable.GetEnumerator() {
                return this.GetEnumerator();
            }

            private class KeyListEnumerator : IEnumerator<String> {
                private HttpClient http;
                private HttpResponseMessage response;
                private Stream contentStream;
                private StreamReader reader;

                public String Current { get; private set; }

                Object IEnumerator.Current => this.Current;

                public KeyListEnumerator(HttpClient http, HttpResponseMessage response) {
                    this.http = http;
                    this.response = response;
                    if (this.response.Content != null) {
                        this.contentStream =
                            this.response.Content.ReadAsStreamAsync()
                                .ConfigureAwait(false)
                                .GetAwaiter()
                                .GetResult();

                        this.reader = new StreamReader(this.contentStream, Encoding.UTF8);
                    }
                }

                public Boolean MoveNext() {
                    if (this.reader != null) {
                        var line = this.reader.ReadLine();

                        if (line != null) {
                            this.Current = Uri.UnescapeDataString(line);
                            return true;
                        } else {
                            this.Current = null;

                            this.reader.Dispose();
                            this.contentStream.Dispose();
                            this.response.Dispose();
                            this.http.Dispose();

                            this.reader = null;
                            this.contentStream = null;
                            this.response = null;
                            this.http = null;

                            return false;
                        }
                    } else {
                        return false;
                    }
                }

                public void Reset() {
                    throw new NotSupportedException();
                }

                public void Dispose() {
                    this.reader?.Dispose();
                    this.contentStream?.Dispose();
                    this.response?.Dispose();
                    this.http?.Dispose();
                }
            }
        }
    }
}
