using MailKit.Security;
using MimeKit;


bool useSsl = true;
var password = File.ReadAllText("%USERPROFILE/secret_gmail.txt");
async Task<IEnumerable<MimeMessage>> GetMessagesAsync () {
	using var imapClient = new MailKit.Net.Imap.ImapClient();
	var secureSocketOptions = SecureSocketOptions.Auto;
	if(useSsl) secureSocketOptions = SecureSocketOptions.SslOnConnect;
	await imapClient.ConnectAsync(host, port, secureSocketOptions);

	await imapClient.AuthenticateAsync(login, password);

	await imapClient.Inbox.OpenAsync(FolderAccess.ReadOnly);

	var uids = await imapClient.Inbox.SearchAsync(SearchQuery.All);

	var messages = new List<MimeMessage>();
	foreach(var uid in uids)
		messages.Add(await imapClient.Inbox.GetMessageAsync(uid));

	imapClient.Disconnect(true);

	return messages;
}