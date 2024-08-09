using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Util;
using Google.Apis.Util.Store;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using MimeKit;

//aaa
const string GMailAccount = "";
var clientSecrets = new ClientSecrets {
	ClientId = "",
	ClientSecret = ""
};
var codeFlow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer {
	DataStore = new FileDataStore("CredentialCacheFolder", false),
	Scopes = ["https://mail.google.com/"],
	ClientSecrets = clientSecrets
});
// Note: For a web app, you'll want to use AuthorizationCodeWebApp instead.
var codeReceiver = new LocalServerCodeReceiver();
var authCode = new AuthorizationCodeInstalledApp(codeFlow, codeReceiver);
var credential = await authCode.AuthorizeAsync(GMailAccount, CancellationToken.None);
if(credential.Token.IsExpired(SystemClock.Default)) {
	await credential.RefreshTokenAsync(CancellationToken.None);
}
var oauth2 = new SaslMechanismOAuth2(credential.UserId, credential.Token.AccessToken);
new Thread(() => {
	using var client = new ImapClient();
	client.Connect("imap.gmail.com", 993, SecureSocketOptions.SslOnConnect);
	client.Authenticate(oauth2);
	client.Inbox.Open(MailKit.FolderAccess.ReadOnly);
	foreach(var a in client.Inbox.Search(SearchOptions.All, SearchQuery.Not(SearchQuery.Seen)).UniqueIds) {
		var msg = client.Inbox.GetMessage(a);
		var from = msg.From.First() as MailboxAddress;
		var subject = msg.Subject;
		Console.WriteLine($"{from.Name, -32}{subject}");
	}
	client.Disconnect(true);
}).Start();