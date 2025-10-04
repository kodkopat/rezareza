using System.Globalization;
using System.Text;
using Microsoft.Playwright;

const string PetitionUrl = "https://iranopasmigirim.com/fa";
const string SignButtonText = "\u062B\u0628\u062A \u0627\u0645\u0636\u0627\u06CC \u0646\u0627\u0634\u0646\u0627\u0633";
const string AlternateSignButtonText = "\u062B\u0628\u062A\u0627\u0645\u0636\u0627\u06CC\u0646\u0627\u0634\u0646\u0627\u0633";
const string SuccessPattern = "\u0627\u0645\u0636\u0627\\s*\u0634\u062F";
const string InstallMarkerFile = ".playwright-install-marker";

Console.OutputEncoding = Encoding.UTF8;

while (true)
{
    ShowMenu();
    var choice = Console.ReadLine()?.Trim();

    if (IsExitChoice(choice))
    {
        Console.WriteLine("Exiting application...");
        return;
    }

    if (IsSignChoice(choice))
    {
        var signatureCount = PromptForSignatureCount();
        if (signatureCount <= 0)
        {
            continue;
        }

        try
        {
            await EnsurePlaywrightInstalledAsync();
            await SignPetitionAsync(signatureCount);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
    else
    {
        Console.WriteLine("Invalid option. Please try again.");
    }
}

static void ShowMenu()
{
    Console.WriteLine();
    Console.WriteLine("==== Main Menu ====");
    Console.WriteLine("1. Sign Reza");
    Console.WriteLine("2. Exit");
    Console.Write("Select an option: ");
}

static bool IsExitChoice(string? choice)
{
    if (string.IsNullOrWhiteSpace(choice))
    {
        return false;
    }

    choice = choice.Trim();
    return choice == "2" || choice.Equals("exit", StringComparison.OrdinalIgnoreCase) || choice.Equals("q", StringComparison.OrdinalIgnoreCase);
}

static bool IsSignChoice(string? choice)
{
    if (string.IsNullOrWhiteSpace(choice))
    {
        return false;
    }

    choice = choice.Trim();
    return choice == "1" || choice.Equals("sign", StringComparison.OrdinalIgnoreCase);
}

static int PromptForSignatureCount()
{
    while (true)
    {
        Console.Write("Enter signature count: ");
        var input = Console.ReadLine();

        if (int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) && count > 0)
        {
            return count;
        }

        Console.WriteLine("Invalid number. Please enter a positive integer.");
    }
}

static async Task EnsurePlaywrightInstalledAsync()
{
    if (File.Exists(InstallMarkerFile))
    {
        return;
    }

    Console.WriteLine("Ensuring Playwright Chromium is installed...");
    var exitCode = Microsoft.Playwright.Program.Main(new[] { "install", "chromium" });

    if (exitCode != 0)
    {
        throw new InvalidOperationException($"Playwright installation failed with exit code {exitCode}.");
    }

    await File.WriteAllTextAsync(InstallMarkerFile, DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
    Console.WriteLine("Chromium is ready.");
}

static async Task SignPetitionAsync(int count)
{
    using var playwright = await Playwright.CreateAsync();
    await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
    {
        Headless = false,
        Timeout = 60000
    });

    for (var index = 1; index <= count; index++)
    {
        Console.WriteLine($"Starting signature {index}...");
        var success = await TrySignOnceAsync(browser, index);

        if (success)
        {
            Console.WriteLine($"Signature {index} succeeded.");
        }
        else
        {
            Console.WriteLine($"Signature {index} failed.");
        }
    }
}

static async Task<bool> TrySignOnceAsync(IBrowser browser, int attemptNumber)
{
    await using var context = await browser.NewContextAsync(new BrowserNewContextOptions
    {
        ViewportSize = null
    });

    var page = await context.NewPageAsync();

    try
    {
        await page.GotoAsync(PetitionUrl, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout = 60000
        });

        var buttonLocator = await FindSignButtonAsync(page);

        await buttonLocator.ScrollIntoViewIfNeededAsync();
        await page.WaitForTimeoutAsync(3000);
        await buttonLocator.ClickAsync(new LocatorClickOptions
        {
            Timeout = 45000
        });

        var successLocator = page.Locator($"text=/{SuccessPattern}/");
        await successLocator.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 45000
        });

        return true;
    }
    catch (TimeoutException ex)
    {
        Console.WriteLine($"Timeout during attempt {attemptNumber}: {ex.Message}");
        return false;
    }
    catch (PlaywrightException ex)
    {
        Console.WriteLine($"Playwright error during attempt {attemptNumber}: {ex.Message}");
        return false;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Unexpected error during attempt {attemptNumber}: {ex.Message}");
        return false;
    }
    finally
    {
        await context.CloseAsync();
    }
}

static async Task<ILocator> FindSignButtonAsync(IPage page)
{
    var candidates = new List<ILocator>
    {
        page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = SignButtonText }),
        page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = AlternateSignButtonText }),
        page.GetByText(SignButtonText, new PageGetByTextOptions { Exact = false }),
        page.GetByText(AlternateSignButtonText, new PageGetByTextOptions { Exact = false })
    };

    foreach (var locator in candidates)
    {
        try
        {
            await locator.WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 5000
            });

            return locator;
        }
        catch (TimeoutException)
        {
            // Try next candidate.
        }
        catch (PlaywrightException)
        {
            // Try next candidate.
        }
    }

    throw new TimeoutException("Could not locate the petition sign button.");
}
