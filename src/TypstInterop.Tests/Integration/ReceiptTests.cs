using System;
using TypstInterop.Abstractions;
using Xunit;

namespace TypstInterop.Tests.Integration;

public class ReceiptTests
{
    private static readonly byte[] MockLogoPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII="
    );

    [Fact]
    public void GenerateReceipt_WithInputsAndLogo_Succeeds()
    {
        // 2. Build and Compile
        using var compiler = new TypstCompiler();
        var result = compiler.Compile(c => c
            .WithFile("logo.png".AsSpan(), MockLogoPng)
            .WithInput("orderId".AsSpan(), "ORD-2026-001".AsSpan())
            .WithInput("date".AsSpan(), "2026-03-17 12:00".AsSpan())
            .WithInput("customer".AsSpan(), "Jane Smith".AsSpan())
            .WithInput("items".AsSpan(), "Item A: $50.00, Item B: $30.00".AsSpan())
            .WithInput("total".AsSpan(), "$80.00".AsSpan())
            .WithSource(
                """
                            #set page(width: 80mm, height: auto, margin: 5mm)
                            #set text(size: 10pt, font: "DejaVu Sans")

                            #align(center)[
                                #image("logo.png", width: 20mm)
                                #v(2mm)
                                *OFFICIAL RECEIPT*
                            ]

                            #v(5mm)
                            #grid(
                                columns: (1fr, 2fr),
                                gutter: 3mm,
                                [Order ID:], [#sys.inputs.orderId],
                                [Date:], [#sys.inputs.date],
                                [Customer:], [#sys.inputs.customer],
                            )

                            #line(length: 100%, stroke: 0.5pt)
                            #v(2mm)
                            #sys.inputs.items
                            #v(2mm)
                            #line(length: 100%, stroke: 0.5pt)

                            #align(right)[
                                *Total: #sys.inputs.total*
                            ]
                """.AsSpan()
            ));

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.NotNull(result.GetBytes());
        Assert.True(result.GetBytes().Length > 500, "Receipt PDF should have a reasonable size");
    }
}
