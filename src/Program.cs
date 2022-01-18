using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Connector.Authentication;
using TeamsBotTemplate;
using TeamsBotTemplate.Bot;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddTransient<IBot, MyBot>();
builder.Services.AddSingleton<IBotFrameworkHttpAdapter, TeamsBotHttpAdapter>();
builder.Services.AddSingleton<BotFrameworkAuthentication, ConfigurationBotFrameworkAuthentication>();
builder.Services.AddSingleton<Notifier>();

builder.Services.AddSingleton<GraphClient>();

builder.Services.AddRazorPages();
builder.Services.AddMvc(options => options.EnableEndpointRouting = false);

// Add services for state management
builder.Services.AddSingleton<IStorage, MemoryStorage>();
builder.Services.AddSingleton<UserState>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

//app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();


app.UseAuthorization();

app.UseMvc();
app.MapRazorPages();

app.Run();
