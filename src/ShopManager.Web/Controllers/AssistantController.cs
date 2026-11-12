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
    public Task<IActionResult> Shop(string? question, CancellationToken cancellation) => Reply(question, false, cancellation);

    [Authorize(Roles = Roles.Manager), HttpGet]
    public IActionResult Manage() => Render(true);

    [Authorize(Roles = Roles.Manager), HttpPost, EnableRateLimiting("assistant")]
    public Task<IActionResult> Manage(string? question, CancellationToken cancellation) => Reply(question, true, cancellation);

    private IActionResult Render(bool manager)
    {
        ViewBag.Manager = manager;
        return View("Chat");
    }

    private async Task<IActionResult> Reply(string? question, bool manager, CancellationToken cancellation)
    {
        question = question?.Trim();
        if (question is null || question.Length is < 1 or > 500)
            ModelState.AddModelError("", "Nhập câu hỏi từ 1 đến 500 ký tự.");
        else
        {
            ViewBag.Question = question;
            ViewBag.Answer = await assistant.AskAsync(question, manager, cancellation);
            if (string.IsNullOrWhiteSpace((string?)ViewBag.Answer))
                ModelState.AddModelError("", "Trợ lý chưa phản hồi. Hãy kiểm tra Ollama và mô hình đã tải trên máy chạy ShopManager.");
        }
        return Render(manager);
    }
}
