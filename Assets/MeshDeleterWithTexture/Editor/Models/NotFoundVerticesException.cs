using System;

namespace Gatosyocora.MeshDeleterWithTexture.Models
{
    public class NotFoundVerticesException : Exception 
    {
        public NotFoundVerticesException() : base("Not found vertices to delete") { }

        public NotFoundVerticesException(string message) : base(message){}
    }
}
