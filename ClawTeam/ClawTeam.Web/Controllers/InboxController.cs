using Microsoft.AspNetCore.Mvc;
using ClawTeam.Web.Models;
using ClawTeam.Web.Services;

namespace ClawTeam.Web.Controllers;

/// <summary>Inbox / messaging operations between agents.</summary>
public class InboxController : Controller
{
    private readonly string _dataDir;

    public InboxController(IConfiguration config)
    {
        _dataDir = config["ClawTeam:DataDir"] ?? ConfigService.GetDataDir();
    }

    // GET /Inbox/Index/{teamName}
    public IActionResult Index(string id)
    {
        if (string.IsNullOrEmpty(id)) return BadRequest("Team name is required");

        var mailbox = new MailboxManager(_dataDir, id);
        var messages = mailbox.GetEventLog(limit: 100);
        ViewBag.TeamName = id;
        return View(messages);
    }

    // POST /Inbox/Send/{teamName}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Send(string id, SendMessageForm form)
    {
        if (string.IsNullOrEmpty(id)) return BadRequest();

        var mailbox = new MailboxManager(_dataDir, id);

        if (string.IsNullOrEmpty(form.To))
        {
            // Broadcast
            mailbox.Broadcast(form.From, form.Content, form.Type);
        }
        else
        {
            mailbox.Send(form.From, form.To, form.Content, form.Type);
        }

        return RedirectToAction(nameof(Index), new { id });
    }

    // GET /Inbox/Receive/{teamName}?agent={agentName}
    public IActionResult Receive(string id, string agent)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(agent))
            return BadRequest("Team name and agent name are required");

        var mailbox = new MailboxManager(_dataDir, id);
        var messages = mailbox.Receive(agent);
        ViewBag.TeamName = id;
        ViewBag.AgentName = agent;
        return View(messages);
    }

    // GET /Inbox/Peek/{teamName}?agent={agentName}
    public IActionResult Peek(string id, string agent)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(agent))
            return BadRequest();

        var mailbox = new MailboxManager(_dataDir, id);
        var messages = mailbox.Peek(agent);
        return Json(messages);
    }
}
