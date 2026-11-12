using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ShopManager.Web.Models;
using ShopManager.Web.Services;

namespace ShopManager.Web.Controllers;

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class AssistantController(ShopAssistant assistant) : Controller
{
    [Authorize(Roles = Roles.Customer), HttpGet]
    public IActionResult Shop() => Render(false);

    [Authorize(Roles = Roles.Customer), HttpPost, EnableRateLimiting("assistant")]
    public Task<IActionResult> Shop(string? question, bool widget, string? previousQuestion,
        string? previousAnswer, CancellationToken cancellation) =>
        Reply(question, false, widget, previousQuestion, previousAnswer, cancellation);

    [Authorize(Roles = Roles.Manager), HttpGet]
    public IActionResult Manage() => Render(true);

    [Authorize(Roles = Roles.Manager), HttpPost, EnableRateLimiting("assistant")]
    public Task<IActionResult> Manage(string? question, bool widget, string? previousQuestion,
        string? previousAnswer, CancellationToken cancellation) =>
        Reply(question, true, widget, previousQuestion, previousAnswer, cancellation);

    private IActionResult Render(bool manager)
    {
        ViewBag.Manager = manager;
        return View("Chat");
    }

    private async Task<IActionResult> Reply(string? question, bool manager, bool widget, string? previousQuestion,
        string? previousAnswer, CancellationToken cancellation)
    {
        question = question?.Trim();
        if (question is null || question.Length is < 1 or > 500)
            return Failure("Nhập câu hỏi từ 1 đến 500 ký tự.", manager, widget, StatusCodes.Status400BadRequest);

        if (previousQuestion?.Length > 500 || previousAnswer?.Length > 2000)
            return Failure("Nội dung câu hỏi trước quá dài. Hãy mở lại trang để tiếp tục.", manager, widget, StatusCodes.Status400BadRequest);

        var answer = await assistant.AskAsync(question, manager, cancellation, previousQuestion, previousAnswer);
        if (string.IsNullOrWhiteSpace(answer))
            return Failure("Trợ lý chưa phản hồi. Hãy kiểm tra Ollama hoặc thử lại sau.", manager, widget,
                StatusCodes.Status503ServiceUnavailable);

        if (widget) return Json(new { answer });
        ViewBag.Question = question;
        ViewBag.Answer = answer;
        return Render(manager);
    }

    private IActionResult Failure(string message, bool manager, bool widget, int status)
    {
        if (widget) return StatusCode(status, new { error = message });
        ModelState.AddModelError("", message);
        return Render(manager);
    }
}
