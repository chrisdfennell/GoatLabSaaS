using GoatLab.Server.Services.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace GoatLab.Server.Filters;

/// <summary>
/// Requires a valid Google reCAPTCHA v3 token in the X-Recaptcha-Token header.
/// The expected action name (e.g. "login", "register") must match what the
/// browser passed to grecaptcha.execute, otherwise the request is rejected.
///
/// When Recaptcha:SecretKey is unset (dev/test), verification is skipped — so
/// nothing changes locally without a key. In prod this attribute is the only
/// thing standing between attackers and the auth endpoints, so configure the
/// key.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class RequireRecaptchaAttribute : Attribute, IAsyncActionFilter
{
    public string Action { get; }
    public RequireRecaptchaAttribute(string action) => Action = action;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var verifier = context.HttpContext.RequestServices.GetRequiredService<IRecaptchaVerifier>();
        var req = context.HttpContext.Request;

        // Header is the primary path (JSON API / Blazor / SSR fetch). Form
        // field is the fallback for SSR <form method="post"> submissions where
        // the JS interceptor injects a hidden input named g-recaptcha-response.
        var token = req.Headers["X-Recaptcha-Token"].FirstOrDefault();
        if (string.IsNullOrEmpty(token) && req.HasFormContentType)
        {
            token = req.Form["g-recaptcha-response"].FirstOrDefault();
        }

        var ok = await verifier.VerifyAsync(token, Action, context.HttpContext.RequestAborted);
        if (!ok)
        {
            // SSR form posts expect a redirect-with-error rather than a JSON
            // 400 — but we don't know the redirect target generically, so the
            // controller action keeps using ObjectResult and the SSR view can
            // surface the body to the user (or wrap with try/catch in JS).
            context.Result = new ObjectResult(new { error = "Recaptcha verification failed. Please try again." })
            {
                StatusCode = StatusCodes.Status400BadRequest
            };
            return;
        }
        await next();
    }
}
