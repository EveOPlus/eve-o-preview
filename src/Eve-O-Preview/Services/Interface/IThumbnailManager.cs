using EveOPreview.View;
using System;
using System.Collections.Generic;

namespace EveOPreview.Services
{
    public interface IThumbnailManager
    {
        void Start();
        void Stop();

        void UpdateThumbnailsSize();
        void UpdateThumbnailFrames();
        void UpdateThumbnailTitleFont();

        IThumbnailView GetClientByTitle(string title);
        IThumbnailView GetClientByPointer(System.IntPtr ptr);
        IThumbnailView GetActiveClient();
        Dictionary<IntPtr, IThumbnailView> GetAllKnownClients();
    }
}