using Microsoft.Extensions.Logging;
using Microsoft.Graph.Beta;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using System.Text;
using WingetIntune.Graph;
using Svrooij.WinTuner.Proxy.Client;

namespace WingetIntune.Tests.Graph;

public class GraphStoreAppUploaderTests
{

    [Fact]
    public async Task CreateStoreAppAsync_WithValidPackageId_CreatesAppSuccessfully()
    {
        const string packageId = "9NZVDKPMR9RD";
        const string manifestData = @"{
  ""description"": ""Choose the browser that prioritizes you, not their bottom line.\n\nDon't settle for the default browser. When you choose Firefox, you protect your data while supporting the non-profit Mozilla Foundation, whose mission is to build a better internet that's safe and accessible for everyone, everywhere.\n\nJoin the hundreds of millions of people who choose to protect what's important by using Firefox, a web browser designed to be fast, private and safe. \n\nFAST. PRIVATE. SAFE. \nFirefox browser gives you effortless privacy protection with lightning-fast page loads. Enhanced Tracking Protection automatically blocks most trackers from invading your privacy and slowing your web browsing.\n\nPICK UP RIGHT WHERE YOU LEFT OFF \nBrowse on your computer, then seamlessly switch to Firefox on your phone. With Firefox across your devices, you can always access your passwords, bookmarks, and tabs. \n\nTAKE YOUR PASSWORDS ON THE GO\nWith a free password manager built into Firefox, you can easily log into sites across all your devices. The Firefox password manager securely stores your logins, automatically fills in usernames and passwords, and suggests strong passwords when you create a new account.\n\nEDIT PDFS WITH EASE\nView, print and edit PDFs with Firefox's free PDF editor. Add text, sign, and highlight PDF documents right in Firefox. \n\nMULTITASKING MADE EASY \nWith Picture-in-Picture, you can pop videos out in separate, scalable windows that pin to the screen so you can keep watching while you go about your business on other websites or do things outside of Firefox. \n\nBROWSE LIKE NO ONE'S WATCHING\nWhen you browse in private mode, you remain incognito. Firefox shields you from third-party cookies and content trackers and clears your search and browsing history when you close all private windows. \n\nPRIVATELY TRANSLATE WEBPAGES IN REAL-TIME\nWhile other browsers rely on cloud services to translate webpages, Firefox translations are done offline without recording what webpages you translate.\n\nFOCUS WHEN IT MATTERS\nReader View conveniently removes distractions like buttons, ads, background images, and videos while allowing you to customize the layout to fit your reading preferences.\n\nCUSTOMIZE YOUR BROWSING EXPERIENCE\nWith Firefox Add-Ons, you can discover extensions and themes that make browsing even faster, safer and more fun. There are extensions for everyone, from additional privacy tools to tab managers to ad blockers. \n\nCHALLENGING THE STATUS QUO SINCE 1998 \nMozilla created Firefox as a faster, more private alternative to internet browsers like Microsoft Edge and Google Chrome. Our mission-driven company and volunteer community continue to put your privacy above all else."",
  ""iconUrl"": ""https://store-images.s-microsoft.com/image/apps.7279.14473293538384797.bcb417dc-ffbe-444e-9589-e6a25f04ad52.156eed19-aa35-4e69-96a7-c11abd7f887d"",
  ""informationUrl"": ""https://www.mozilla.org/firefox/"",
  ""privacyInformationUrl"": ""https://www.mozilla.org/privacy/firefox/"",
  ""scope"": ""user"",
  ""architectures"": [
    ""x64"",
    ""x86"",
    ""arm64""
  ],
  ""packageIdentifier"": ""9NZVDKPMR9RD"",
  ""displayName"": ""Mozilla Firefox"",
  ""publisher"": ""Mozilla""
}";
        const string graphResponseData = @"{ ""@odata.type"": ""#microsoft.graph.winGetApp"", ""id"": ""0177548a-548a-0177-8a54-77018a547701"" }";
        var httpClient = Substitute.For<HttpClient>();
        var graphLogger = Substitute.For<ILogger<GraphStoreAppUploader>>();
        var fileManager = Substitute.For<IFileManager>();
        var proxyClient = new WinTunerProxyClient(new HttpClientRequestAdapter(new AnonymousAuthenticationProvider(), httpClient: httpClient));
        var storeAppUploader = new GraphStoreAppUploader(graphLogger, fileManager, proxyClient);
        var cancellationToken = new CancellationToken();

        var manifestResponse = new HttpResponseMessage
        {
            Content = new StringContent(manifestData, Encoding.UTF8, "application/json")
        };

        var graphResponse = new HttpResponseMessage
        {
            Content = new StringContent(graphResponseData, Encoding.UTF8, "application/json")
        };

        httpClient.SendAsync(Arg.Is<HttpRequestMessage>(req =>
            req.Method == HttpMethod.Get
            && req.RequestUri == new Uri($"https://proxy.wintuner.app/api/store/package/{packageId}")), cancellationToken)
            .Returns(manifestResponse);

        fileManager.DownloadFileAsync("https://store-images.s-microsoft.com/image/apps.7279.14473293538384797.bcb417dc-ffbe-444e-9589-e6a25f04ad52.156eed19-aa35-4e69-96a7-c11abd7f887d", Arg.Any<string>(), expectedHash: null, overrideFile: false, cancellationToken: cancellationToken)
                        .Returns(Task.CompletedTask);

        fileManager.ReadAllBytesAsync(Arg.Any<string>(), cancellationToken)
            .Returns(Encoding.UTF8.GetBytes("fake image"));

        var expectedGraphBody = new Dictionary<string, object>
        {
            { "@odata.type", "#microsoft.graph.winGetApp" },
            { "displayName", "Mozilla Firefox" },
            { "publisher", "Mozilla" }
        };
        httpClient.SendAsync(Arg.Is<HttpRequestMessage>(req =>
            req.Method == HttpMethod.Post
            && req.RequestUri.ToString().Equals("https://graph.microsoft.com/beta/deviceAppManagement/mobileApps", StringComparison.OrdinalIgnoreCase)
            && req.Content != null
            && req.Content.ValidateJsonBody(expectedGraphBody)
            ), cancellationToken)
            .Returns(graphResponse);

        var graphClient = new GraphServiceClient(httpClient, new AnonymousAuthenticationProvider());

        var result = await storeAppUploader.CreateStoreAppAsync(graphClient, packageId, cancellationToken);

        Assert.NotNull(result);
        Assert.Equal("0177548a-548a-0177-8a54-77018a547701", result?.Id);

    }

    [Fact]
    public async Task GetStoreIdForNameAsync_Returns_ExptectedResult()
    {
        var httpClient = Substitute.For<HttpClient>();
        var graphStoreLogger = Substitute.For<ILogger<GraphStoreAppUploader>>();
        var proxyClient = new WinTunerProxyClient(new HttpClientRequestAdapter(new AnonymousAuthenticationProvider(), httpClient: httpClient));
        var cancellationToken = new CancellationToken();

        var graphStoreAppUploader = new GraphStoreAppUploader(graphStoreLogger, Substitute.For<IFileManager>(), proxyClient);

        var expectedResponse = new HttpResponseMessage
        {
            Content = new StringContent(@"[{""packageIdentifier"": ""9NZVDKPMR9RD"",""displayName"": ""Mozilla Firefox"",""publisher"": ""Mozilla""}]", Encoding.UTF8, "application/json")
        };

        // Validate the body of the request somehow
        httpClient.SendAsync(Arg.Is<HttpRequestMessage>(req =>
            req.Method == HttpMethod.Get
            && req.RequestUri == new Uri("https://proxy.wintuner.app/api/store/search?searchString=Mozilla%20Firefox")), cancellationToken)
            .Returns(expectedResponse);

        // Act
        var result = await graphStoreAppUploader.GetStoreIdForNameAsync("Mozilla Firefox", cancellationToken);

        // Assert
        Assert.Equal("9NZVDKPMR9RD", result);
    }
}
