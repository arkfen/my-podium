using MyPodium.Components;
using Microsoft.Extensions.Azure;
using MyPodium.Services;
using System.Net;
using System.Net.Mail;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddAzureClients(clientBuilder =>
{
    clientBuilder.AddBlobServiceClient(builder.Configuration["StorageConnection:blobServiceUri"]!).WithName("StorageConnection");
    clientBuilder.AddQueueServiceClient(builder.Configuration["StorageConnection:queueServiceUri"]!).WithName("StorageConnection");
    clientBuilder.AddTableServiceClient(builder.Configuration["StorageConnection:tableServiceUri"]!).WithName("StorageConnection");
});

// Add email service
builder.Services.AddTransient<IEmailService>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var smtpServer = config["EmailSettings:SmtpServer"] ?? throw new InvalidOperationException("SMTP server configuration missing");
    var smtpPort = int.Parse(config["EmailSettings:SmtpPort"] ?? "587");
    var smtpUsername = config["EmailSettings:Username"] ?? throw new InvalidOperationException("SMTP username configuration missing");
    var smtpPassword = config["EmailSettings:Password"] ?? throw new InvalidOperationException("SMTP password configuration missing");
    var senderEmail = config["EmailSettings:SenderEmail"] ?? throw new InvalidOperationException("Sender email configuration missing");
    var senderName = config["EmailSettings:SenderName"] ?? throw new InvalidOperationException("Sender name configuration missing");
    
    return new EmailService(smtpServer, smtpPort, smtpUsername, smtpPassword, senderEmail, senderName);
});

// Add auth service
builder.Services.AddScoped<AuthService>();

// Add admin auth service
builder.Services.AddScoped<AdminAuthService>();

// Add protected browser storage
builder.Services.AddServerSideBlazor().AddHubOptions(options => {
    options.MaximumReceiveMessageSize = 64 * 1024; // 64 KB
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
