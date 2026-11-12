using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TypstInterop.Models;
using Xunit;

namespace TypstInterop.Tests.Features;

/// <summary>
/// Disposal semantics and stream-based input handling.
/// </summary>
public class LifetimeTests
{
    [Fact]
    public void Dispose_IsIdempotent_AndLaterCallsThrow()
    {
        var compiler = new TypstCompiler();
        compiler.Dispose();
        compiler.Dispose();

        Assert.Throws<ObjectDisposedException>(() => compiler.Compile(c => c.WithSource("= Hi")));
        Assert.Throws<ObjectDisposedException>(() => compiler.ListFonts());
    }

    [Fact]
    public async Task Dispose_DuringConcurrentCompiles_NeverFreesWorldInUse()
    {
        var compiler = new TypstCompiler(new TypstCompilerOptions
        {
            FontsSource = TypstFontsSource.DefaultOnly,
            PackagesSource = TypstPackagesSource.None,
        });
        using var start = new ManualResetEventSlim();
        var ct = TestContext.Current.CancellationToken;

        var workers = new Task[4];
        for (var i = 0; i < workers.Length; i++)
        {
            workers[i] = Task.Run(() =>
            {
                start.Wait(ct);
                for (var n = 0; n < 5; n++)
                {
                    try
                    {
                        var result = compiler.Compile(c => c.WithSource("= Hello\n#lorem(200)"));
                        Assert.True(result.IsSuccess, result.ErrorMessage);
                    }
                    catch (ObjectDisposedException)
                    {
                        return;
                    }
                }
            }, ct);
        }

        start.Set();
        await Task.Delay(50, ct);
        compiler.Dispose();
        await Task.WhenAll(workers);
    }

    [Fact]
    public void WithFile_Stream_ReadsFromCurrentPosition()
    {
        using var compiler = new TypstCompiler();
        var payload = Encoding.UTF8.GetBytes("IGNOREDpayload-text");
        using var stream = new MemoryStream(payload);
        stream.Position = "IGNORED".Length;

        var result = compiler.Compile(c => c
            .WithFile("data.txt", stream)
            .WithSource("#read(\"data.txt\")")
            .WithFormat(TypstOutputFormat.Html));

        Assert.True(result.IsSuccess, result.ErrorMessage);
        var html = Encoding.UTF8.GetString(result.Output.ToArray());
        Assert.Contains("payload-text", html);
        Assert.DoesNotContain("IGNORED", html);
    }

    [Fact]
    public void Today_RespectsOffset()
    {
        // UTC-12 and UTC+14 are 26 hours apart, so their dates always differ.
        // A World::today that ignores the offset would yield the same date.
        using var compiler = new TypstCompiler();
        var result = compiler.Compile(c => c
            .WithSource(
                """
                #let fmt = "[year]-[month]-[day]"
                #datetime.today(offset: -12).display(fmt)|#datetime.today(offset: 14).display(fmt)
                """)
            .WithFormat(TypstOutputFormat.Html));

        Assert.True(result.IsSuccess, result.ErrorMessage);
        var html = Encoding.UTF8.GetString(result.Output.ToArray());
        var match = System.Text.RegularExpressions.Regex.Match(html, @"(\d{4}-\d{2}-\d{2})\|(\d{4}-\d{2}-\d{2})");
        Assert.True(match.Success, html);
        var west = DateTime.Parse(match.Groups[1].Value);
        var east = DateTime.Parse(match.Groups[2].Value);
        Assert.InRange((east - west).TotalDays, 1, 2);
    }
}
