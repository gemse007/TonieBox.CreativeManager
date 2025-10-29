using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Net.Http.Headers;
using System.Text;
using System.Text.Unicode;
using System.Threading.Tasks;
using TonieCreativeManager.Service;

namespace TonieCreativeManager.Ui2.Pages
{
    [ResponseCache(Location = ResponseCacheLocation.Client, Duration = 3600 * 7)]
    public class Cover : PageModel
    {
        private readonly MediaService mediaService;

        public Cover(MediaService mediaService)
        {
            this.mediaService = mediaService;
        }

        public async Task<FileStreamResult?> OnGetAsync()
        {
            try
            {
                var path = Request.Query["path"];

                var cover = await mediaService.GetCoverAsync(path.ToString().DecodeUrl());

                return new FileStreamResult(cover.Data, new MediaTypeHeaderValue(cover.MimeType));
            }
            catch
            {
            }
            var emptyPng = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChAI9jU77zgAAAABJRU5ErkJggg==");
            return new FileStreamResult(new MemoryStream(emptyPng), "image/png");
        }
    }
}
