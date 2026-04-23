using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SysFile = System.IO.File;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace PSNChecker
{
    class Program
    {
        static ITelegramBotClient _bot;
        static long _chatId;
        static CancellationTokenSource _genCts;
        static Task _genTask;
        static readonly object _lock = new object();
        static int _checked = 0;
        static int _found = 0;

        static readonly HttpClient _http = new HttpClient(new HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
        });

        static readonly string[] Prefixes = new[]
        {
            "After","Dark","Shadow","Silent","Toxic","Savage","Royal","Mythic","Frost","Storm",
            "Venom","Phantom","Rogue","Blood","Steel","Soul","Night","Wild","Mad","Iron",
            "Fire","Ghost","Demon","Holy","Lone","Cold","Brutal","Cyber","Neon","Blaze",
            "Reaper","Hunter","Killer","Vortex","Zero","Alpha","Hyper","Elite"
        };

        static readonly string[] Suffixes = new[]
        {
            "King","Lord","Knight","Beast","Wolf","Lion","Tiger","Dragon","Reaper","Demon",
            "Ghost","Storm","Fire","Shadow","Hawk","Blade","Strike","Force","Rage","Fury",
            "Slayer","Hunter","Killer","Master","Sniper","Ninja","Samurai","Warrior","Phantom","Soul"
        };

        static readonly string[] CuratedNames = new[]
        {
            "AfterKill","AfterDeath","AfterLife","AfterDark","AfterStorm","AfterShadow","AfterSlayer","AfterReaper",
            "DarkSoul","DarkKnight","DarkLord","DarkReaper","DarkPhantom","DarkBlade","DarkHunter","DarkSlayer","DarkStorm",
            "ShadowKing","ShadowHunter","ShadowReaper","ShadowBlade","ShadowKnight","ShadowLord","ShadowPhantom",
            "SilentKiller","SilentReaper","SilentBlade","SilentDeath","SilentHunter","SilentStorm",
            "ToxicReaper","ToxicSlayer","ToxicBlade","ToxicPhantom","ToxicKing",
            "PhantomLord","PhantomKnight","PhantomReaper","PhantomBlade","PhantomKing",
            "BloodReaper","BloodLord","BloodKnight","BloodHunter","BloodSlayer","BloodMoon","BloodStorm",
            "SoulReaper","SoulHunter","SoulSlayer","SoulKnight","SoulBlade","SoulKing",
            "StormBlade","StormReaper","StormKing","StormLord","StormHunter",
            "FireBlade","FireStorm","FireDemon","FireReaper","FireKing","FireLord",
            "FrostBlade","FrostReaper","FrostDemon","FrostKnight","FrostKing","FrostHunter",
            "NightKing","NightLord","NightHunter","NightBlade","NightReaper","NightPhantom",
            "WildHunter","WildReaper","WildKing","WildBeast","WildStorm",
            "MadKing","MadHunter","MadReaper","MadLord",
            "IronWolf","IronBlade","IronKnight","IronLord","IronKing","IronBeast",
            "GhostBlade","GhostReaper","GhostHunter","GhostKnight","GhostLord","GhostKing",
            "ReaperKing","ReaperLord","ReaperBlade",
            "DemonSlayer","DemonHunter","DemonKing","DemonLord","DemonBlade",
            "DragonSlayer","DragonHunter","DragonKing","DragonLord","DragonBlade",
            "WolfHunter","WolfKing","WolfReaper","WolfLord",
            "ZeroHunter","ZeroBlade","ZeroReaper","ZeroKnight",
            "RoyalKnight","RoyalReaper","RoyalBlade","RoyalLord",
            "MythicBlade","MythicHunter","MythicReaper","MythicKing",
            "CyberKnight","CyberBlade","CyberReaper","CyberHunter",
            "NeonReaper","NeonBlade","NeonKnight",
            "BlazeBlade","BlazeReaper","BlazeKnight",
            "VenomBlade","VenomReaper","VenomKnight","VenomLord",
            "RogueBlade","RogueHunter","RogueKnight","RogueReaper",
            "SteelBlade","SteelKnight","SteelLord","SteelKing",
            "ColdBlade","ColdReaper","ColdHunter","ColdLord",
            "BrutalReaper","BrutalKing","BrutalLord","BrutalBlade",
            "SavageBlade","SavageKing","SavageHunter","SavageReaper",
            "EliteHunter","EliteSniper","EliteBlade","EliteReaper",
            "HolyKnight","HolyBlade","HolyKing","HolyReaper",
            "AlphaWolf","AlphaHunter","AlphaKing","AlphaReaper",
            "HyperBlade","HyperReaper","HyperKnight",
            "VortexBlade","VortexReaper","VortexKnight",
            "LoneWolf","LoneHunter","LoneReaper","LoneKnight"
        };

        static readonly Random _rand = new Random();
        static readonly string _gifPath = Path.Combine(AppContext.BaseDirectory, "assets", "welcome.gif");
        static string _gifFileId = null;

        static async Task Main(string[] args)
        {
            var token = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN");
            if (string.IsNullOrWhiteSpace(token))
            {
                Console.WriteLine("ERROR: TELEGRAM_BOT_TOKEN environment variable is required.");
                return;
            }

            _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (PlayStation; PlayStation 5/2.50) AppleWebKit/605.1.15 (KHTML, like Gecko)");
            _http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            _http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
            _http.Timeout = TimeSpan.FromSeconds(15);

            _bot = new TelegramBotClient(token);
            var me = await _bot.GetMeAsync();
            Console.WriteLine($"Bot started: @{me.Username}");

            using var cts = new CancellationTokenSource();
            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = new[] { UpdateType.Message, UpdateType.CallbackQuery }
            };
            _bot.StartReceiving(HandleUpdateAsync, HandleErrorAsync, receiverOptions, cts.Token);

            // Run forever — bot waits silently for /start from any user
            await Task.Delay(-1);
        }

        static InlineKeyboardMarkup BuildMenu()
        {
            return new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Run", "start"),
                    InlineKeyboardButton.WithCallbackData("Stop", "stop"),
                }
            });
        }

        static async Task SendWelcome()
        {
            try
            {
                if (_gifFileId != null)
                {
                    await _bot.SendAnimationAsync(_chatId, InputFile.FromFileId(_gifFileId),
                        replyMarkup: BuildMenu());
                    return;
                }
                if (SysFile.Exists(_gifPath))
                {
                    using var fs = SysFile.OpenRead(_gifPath);
                    var msg = await _bot.SendAnimationAsync(_chatId,
                        InputFile.FromStream(fs, "welcome.gif"),
                        replyMarkup: BuildMenu());
                    _gifFileId = msg.Animation?.FileId ?? msg.Document?.FileId;
                }
                else
                {
                    await _bot.SendTextMessageAsync(_chatId, "👇", replyMarkup: BuildMenu());
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("SendWelcome error: " + e.Message);
            }
        }

        static async Task SendMenu(string text)
        {
            try
            {
                await _bot.SendTextMessageAsync(_chatId, text, replyMarkup: BuildMenu());
            }
            catch (Exception e)
            {
                Console.WriteLine("SendMenu error: " + e.Message);
            }
        }

        static async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
        {
            try
            {
                if (update.Message != null && update.Message.Text != null)
                {
                    _chatId = update.Message.Chat.Id;
                    var txt = update.Message.Text.Trim();
                    if (txt.StartsWith("/start") || txt.StartsWith("/menu"))
                    {
                        await SendWelcome();
                    }
                    else if (txt.StartsWith("/status"))
                    {
                        await SendStatus();
                    }
                }
                else if (update.CallbackQuery != null)
                {
                    if (update.CallbackQuery.Message != null)
                        _chatId = update.CallbackQuery.Message.Chat.Id;
                    var data = update.CallbackQuery.Data;
                    await bot.AnswerCallbackQueryAsync(update.CallbackQuery.Id);
                    if (data == "start") await StartGeneration();
                    else if (data == "stop") await StopGeneration();
                    else if (data == "status") await SendStatus();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("HandleUpdate error: " + e);
            }
        }

        static Task HandleErrorAsync(ITelegramBotClient bot, Exception ex, CancellationToken ct)
        {
            Console.WriteLine("Telegram polling error: " + ex.Message);
            return Task.CompletedTask;
        }

        static Task StartGeneration()
        {
            lock (_lock)
            {
                if (_genTask != null && !_genTask.IsCompleted) return Task.CompletedTask;
                _genCts = new CancellationTokenSource();
                _checked = 0;
                _found = 0;
                _genTask = Task.Run(() => GenerationLoop(_genCts.Token));
            }
            return Task.CompletedTask;
        }

        static async Task StopGeneration()
        {
            CancellationTokenSource cts;
            Task t;
            lock (_lock)
            {
                cts = _genCts;
                t = _genTask;
            }
            if (cts == null || t == null || t.IsCompleted) return;
            cts.Cancel();
            try { await t; } catch { }
        }

        static async Task SendStatus()
        {
            bool running = _genTask != null && !_genTask.IsCompleted;
            string msg = $"الحالة: {(running ? "يعمل ▶️" : "متوقف ⏹")}\nتم فحص: {_checked}\nتم العثور: {_found}";
            await _bot.SendTextMessageAsync(_chatId, msg, replyMarkup: BuildMenu());
        }

        static string GenerateUsername()
        {
            // 70% curated handcrafted names, 30% compositional from refined word lists
            if (_rand.Next(10) < 7)
            {
                return CuratedNames[_rand.Next(CuratedNames.Length)];
            }
            var p = Prefixes[_rand.Next(Prefixes.Length)];
            var s = Suffixes[_rand.Next(Suffixes.Length)];
            return p + s;
        }

        static async Task GenerationLoop(CancellationToken ct)
        {
            var seen = new HashSet<string>();
            var seenLock = new object();
            const int parallel = 6;
            var sem = new SemaphoreSlim(parallel, parallel);
            var tasks = new List<Task>();

            while (!ct.IsCancellationRequested)
            {
                string name = GenerateUsername();
                lock (seenLock) { if (!seen.Add(name)) continue; }
                if (name.Length < 3 || name.Length > 16) continue;

                await sem.WaitAsync(ct);
                tasks.Add(Task.Run(async () =>
                {
                    try { await ProcessName(name, ct); }
                    finally { sem.Release(); }
                }, ct));

                tasks.RemoveAll(t => t.IsCompleted);
            }
            try { await Task.WhenAll(tasks); } catch { }
        }

        static async Task ProcessName(string name, CancellationToken ct)
        {
            var result = await CheckPsn(name, ct);
            Interlocked.Increment(ref _checked);
            Console.WriteLine($"{name} -> {result}");

            if (result != CheckResult.Available) return;

            // Double-confirm to ensure 100% accuracy
            await Task.Delay(250, ct);
            var confirm = await CheckPsn(name, ct);
            if (confirm != CheckResult.Available)
            {
                Console.WriteLine($"{name} -> not confirmed ({confirm})");
                return;
            }

            Interlocked.Increment(ref _found);
            try
            {
                var caption = $"`{name}`";
                if (_gifFileId != null)
                {
                    await _bot.SendAnimationAsync(_chatId, InputFile.FromFileId(_gifFileId),
                        caption: caption, parseMode: ParseMode.Markdown, cancellationToken: ct);
                }
                else if (SysFile.Exists(_gifPath))
                {
                    using var fs = SysFile.OpenRead(_gifPath);
                    var msg = await _bot.SendAnimationAsync(_chatId,
                        InputFile.FromStream(fs, "welcome.gif"),
                        caption: caption, parseMode: ParseMode.Markdown, cancellationToken: ct);
                    _gifFileId = msg.Animation?.FileId ?? msg.Document?.FileId;
                }
                else
                {
                    await _bot.SendTextMessageAsync(_chatId, caption,
                        parseMode: ParseMode.Markdown, cancellationToken: ct);
                }
            }
            catch (Exception e) { Console.WriteLine("Notify error: " + e.Message); }
        }

        enum CheckResult { Available, Taken, Invalid, Unknown }

        static async Task<CheckResult> CheckPsn(string psn, CancellationToken ct)
        {
            try
            {
                var payload = JsonConvert.SerializeObject(new { onlineId = psn, reserveIfAvailable = false });
                using var req = new HttpRequestMessage(HttpMethod.Post,
                    "https://accounts.api.playstation.com/api/v1/accounts/onlineIds");
                req.Content = new StringContent(payload, Encoding.UTF8, "application/json");
                using var resp = await _http.SendAsync(req, ct);
                var body = await resp.Content.ReadAsStringAsync(ct);

                if (resp.IsSuccessStatusCode) return CheckResult.Available;

                // Try parse JSON error
                try
                {
                    dynamic obj = JsonConvert.DeserializeObject(body);
                    if (obj != null)
                    {
                        string msg = (string)(obj[0]?.validationErrors?[0]?.message ?? "");
                        if (!string.IsNullOrEmpty(msg))
                        {
                            var lower = msg.ToLowerInvariant();
                            if (lower.Contains("taken") || lower.Contains("not available") || lower.Contains("already")) return CheckResult.Taken;
                            return CheckResult.Invalid;
                        }
                    }
                }
                catch { /* non-json (HTML challenge etc.) */ }

                return CheckResult.Unknown;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                Console.WriteLine($"CheckPsn error for {psn}: {e.Message}");
                return CheckResult.Unknown;
            }
        }
    }
}
