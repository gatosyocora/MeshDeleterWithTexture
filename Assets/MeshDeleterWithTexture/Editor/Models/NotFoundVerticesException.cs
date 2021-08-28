using System;

namespace Gatosyocora.MeshDeleterWithTexture.Models
{
    public class NotFoundVerticesException : Exception 
    {
        public NotFoundVerticesException(string message) : base(message){}
    }
}
