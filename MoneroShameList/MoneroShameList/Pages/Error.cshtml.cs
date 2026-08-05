using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MoneroShameList.Pages
{
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [IgnoreAntiforgeryToken]
    public class ErrorModel : PageModel
    {
        /// <summary>The HTTP status being shown (from ?code=, set by the status-code middleware).</summary>
        public int Code { get; private set; } = 404;

        public bool IsNotFound => this.Code == 404;

        // Do NOT set Response.StatusCode here — UseStatusCodePagesWithReExecute preserves the
        // original status automatically, and setting it would re-trigger the middleware.
        public void OnGet(int? code)
        {
            // ?code from the status-code middleware; otherwise the current status (e.g. 500
            // set by the exception handler). Falls back to 500 for anything unexpected.
            var status = code ?? (this.Response.StatusCode >= 400 ? this.Response.StatusCode : 500);
            this.Code = status is >= 400 and < 600 ? status : 500;
        }
    }
}
