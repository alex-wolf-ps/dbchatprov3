using Azure.AI.OpenAI;
using Azure.Identity;
using DBChatPro;
using DBChatPro.Components;
using DBChatPro.Services;
using Microsoft.Extensions.Azure;
using Microsoft.Identity.Client.Platforms.Features.DesktopOs.Kerberos;
using MudBlazor;
using MudBlazor.Services;
using OpenAI;
using System.ClientModel;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

// Core services regardless where the app is running
builder.Services.AddScoped<OpenAIService>();
builder.Services.AddScoped<DatabaseService>();

// Uncomment for vanilla OpenAI (non azure)
// builder.Services.AddScoped<OpenAIClient>(factory =>
// {
//     return new OpenAIClient(new ApiKeyCredential("your-openai-key"));
// });

// For Azure OpenAI using Entra ID
#region Credential chain
// Build up credential chain for cloud and local tooling options
var userAssignedIdentityCredential = 
    new ManagedIdentityCredential(builder.Configuration.GetValue<string>("AZURE_CLIENT_ID"));
    
var visualStudioCredential = new VisualStudioCredential(
    new VisualStudioCredentialOptions()
    { 
        TenantId = builder.Configuration.GetValue<string>("AZURE_TENANT_ID") 
    });

var azureDevCliCredential = new AzureDeveloperCliCredential(
    new AzureDeveloperCliCredentialOptions()
    {
        TenantId = builder.Configuration.GetValue<string>("AZURE_TENANT_ID")
    });

var azureCliCredential = new AzureCliCredential(
    new AzureCliCredentialOptions()
    {
        TenantId = builder.Configuration.GetValue<string>("AZURE_TENANT_ID")
    });

var credential = new ChainedTokenCredential(userAssignedIdentityCredential, azureDevCliCredential, visualStudioCredential, azureCliCredential);
#endregion

// Use in-memory services in local mode
if (builder.Configuration["EnvironmentMode"] == "local")
{
    builder.Services.AddSingleton<IQueryService, InMemoryQueryService>();
    builder.Services.AddSingleton<IConnectionService, InMemoryConnectionService>();

    var azureOpenAIEndpoint = new Uri(builder.Configuration["AZURE_OPENAI_ENDPOINT"]);

    // Comment out this AddAzureClients section if you're using vanilla OpenAI instead of Azure OpenAI
    builder.Services.AddAzureClients(async clientBuilder =>
        {
            clientBuilder.AddClient<AzureOpenAIClient, AzureOpenAIClientOptions>(
                (options, _, _) => new AzureOpenAIClient(
                    azureOpenAIEndpoint, credential, options)); // Replace "credential" with new ApiKeyCredential("your-key") to use key based auth with Azure
        });
}
// Use Azure services in hosted mode
else if (builder.Configuration["EnvironmentMode"] == "hosted")
{
    var azureOpenAIEndpoint = new Uri(builder.Configuration["AZURE_OPENAI_ENDPOINT"]);
    var azureTableEndpoint = new Uri(builder.Configuration["AZURE_STORAGE_ENDPOINT"]);
    var azureKeyVaultEndpoint = new Uri(builder.Configuration["AZURE_KEYVAULT_ENDPOINT"]);

    builder.Services.AddAzureClients(async clientBuilder =>
    {
        // Register the table storage and key vault services
        clientBuilder.AddTableServiceClient(azureTableEndpoint);
        clientBuilder.AddSecretClient(azureKeyVaultEndpoint);

        // Comment this AddClient block out if you're using vanilla OpenAI instead of Azure OpenAI
        clientBuilder.AddClient<AzureOpenAIClient, AzureOpenAIClientOptions>(
            (options, _, _) => new AzureOpenAIClient(
                azureOpenAIEndpoint, credential, options)); // Replace "credential" with new ApiKeyCredential("your-key") to use key based auth with Azure

        clientBuilder.UseCredential(credential);
    });

    builder.Services.AddScoped<IQueryService, AzureTableQueryService>();
    builder.Services.AddScoped<IConnectionService, AzureKeyVaultConnectionService>();
}

// Mudblazor stuff
builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomLeft;

    config.SnackbarConfiguration.PreventDuplicates = false;
    config.SnackbarConfiguration.NewestOnTop = false;
    config.SnackbarConfiguration.ShowCloseIcon = true;
    config.SnackbarConfiguration.VisibleStateDuration = 10000;
    config.SnackbarConfiguration.HideTransitionDuration = 500;
    config.SnackbarConfiguration.ShowTransitionDuration = 500;
    config.SnackbarConfiguration.SnackbarVariant = Variant.Filled;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode();

app.Run();
