using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ReplitDbClient {
    public static class ReplitDbClientExtensions {
        public static Task<IEnumerable<String>> GetAll(this IReplitDbClient client) {
            return Task.FromResult<IEnumerable<String>>(new DbValueEnumerable(client));
        }

        public static async Task DeleteMultiple(this IReplitDbClient client, params String[] keys) {
            foreach (var key in keys) {
                await client.Delete(key).ConfigureAwait(false);
            }
        }

        public static async Task Empty(this IReplitDbClient client) {
            foreach (var key in await client.ListKeys().ConfigureAwait(false)) {
                await client.Delete(key).ConfigureAwait(false);
            }
        }

        private class DbValueEnumerable : IEnumerable<String> {
            private readonly IReplitDbClient client;

            public DbValueEnumerable(IReplitDbClient client) {
                this.client = client;
            }

            public IEnumerator<String> GetEnumerator() {
                var keyEnumerator =
                    this.client.ListKeys().ConfigureAwait(false).GetAwaiter().GetResult().GetEnumerator();

                return new DbValueEnumerator(this.client, keyEnumerator);
            }

            IEnumerator IEnumerable.GetEnumerator() {
                return this.GetEnumerator();
            }

            private class DbValueEnumerator : IEnumerator<String> {
                private readonly IReplitDbClient client;
                private IEnumerator<String> keyEnumerator;

                public String Current { get; private set; }

                Object IEnumerator.Current => this.Current;

                public DbValueEnumerator(IReplitDbClient client, IEnumerator<String> keyEnumerator) {
                    this.client = client;
                    this.keyEnumerator = keyEnumerator;
                }

                public Boolean MoveNext() {
                    if (this.keyEnumerator != null) {
                        if (this.keyEnumerator.MoveNext()) {
                            this.Current =
                                this.client.Get(this.keyEnumerator.Current)
                                    .ConfigureAwait(false)
                                    .GetAwaiter()
                                    .GetResult();

                            return true;
                        } else {
                            this.keyEnumerator.Dispose();
                            this.keyEnumerator = null;
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
                    this.keyEnumerator?.Dispose();
                }
            }
        }
    }
}
