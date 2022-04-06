using System;
using System.Net;
using System.Runtime.Serialization;

namespace ReplitDbClient {
    public class ServiceException : Exception {
        public HttpStatusCode Status { get; }

        public ServiceException(String message, HttpStatusCode status) : base(message) {
            this.Status = status;
        }

        protected ServiceException(SerializationInfo info, StreamingContext context) : base(info, context) {
            this.Status = (HttpStatusCode)info.GetInt32(nameof(this.Status));
        }

        public override void GetObjectData(
            SerializationInfo info,
            StreamingContext context) {

            base.GetObjectData(info, context);
            info.AddValue(nameof(this.Status), (Int32)this.Status);
        }
    }
}
