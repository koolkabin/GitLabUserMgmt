using GitlabServiceClient;
using Microsoft.OpenApi.Models;
using Refit;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

//enable built-in http loggin
builder.Services.AddHttpLogging(logging =>
{
    logging.LoggingFields = Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.All;
});

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    // Add Bearer token security definition to Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        BearerFormat = "JWT",
        Description = "Please enter token in the format 'Bearer {your token}'"
    });

    // Add security requirement to use Bearer token for all API calls
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] { }
        }
    });
});

// Register Refit client with authentication
builder.Services.AddRefitClient<IGitLabApi>()
    .ConfigureHttpClient(c => c.BaseAddress = new Uri("https://gitlab.com/api/v4"))
    .ConfigureHttpClient(client =>
    {
        // Optional: Set headers if needed
        client.DefaultRequestHeaders.Add("User-Agent", "MyGitLabClient");
    })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()) // Optional custom handler
    .ConfigureHttpClient((serviceProvider, httpClient) =>
    {
        // Configure JSON settings to be case insensitive
        var settings = new RefitSettings
        {
            ContentSerializer = new SystemTextJsonContentSerializer(new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            })
        };

        serviceProvider.GetRequiredService<IHttpClientFactory>()
            .CreateClient()
            .BaseAddress = new Uri("https://gitlab.com/api/v4");
    });

var app = builder.Build();


// Configure the HTTP request pipeline.
if (true || app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();


app.MapGet("/user", async (HttpContext context, IGitLabApi gitLabApi) =>
{
    string? token = context.Request.Headers["Authorization"];
    if (string.IsNullOrEmpty(token))
    {
        return Results.BadRequest("Authorization token is required");
    }

    if (!token.StartsWith("Bearer "))
    {
        token = $"Bearer {token}";
    }
    var users = await gitLabApi.GetAuthenticatedUserAsync(token);

    if (users == null)
    {
        return Results.NotFound($"Cannot find logged in user info.");
    }

    return Results.Ok(users); // GitLab API returns a list, but we only need the first match.
});
app.MapGet("/user/{username}", async (string username, HttpContext context, IGitLabApi gitLabApi) =>
{
    var users = await gitLabApi.GetUserByUserNameAsync(username);

    if (users == null || users.Count == 0)
    {
        return Results.NotFound($"User '{username}' not found.");
    }

    return Results.Ok(users.First()); // GitLab API returns a list, but we only need the first match.
});

app.MapGet("/projects", async (HttpContext context, IGitLabApi gitLabApi) =>
{
    string? token = context.Request.Headers["Authorization"];
    if (string.IsNullOrEmpty(token))
    {
        return Results.BadRequest("Authorization token is required");
    }

    if (!token.StartsWith("Bearer "))
    {
        token = $"Bearer {token}";
    }

    // Get page and per_page from query parameters, with default values
    int page = int.TryParse(context.Request.Query["page"], out var parsedPage) ? parsedPage : 1;
    int perPage = int.TryParse(context.Request.Query["per_page"], out var parsedPerPage) ? parsedPerPage : 10;

    var projects = await gitLabApi.GetOwnedProjectsAsync(page, perPage, token);

    return Results.Ok(new
    {
        page,
        perPage,
        projects
    });
});
app.MapGet("/projects/{id}", async (int id, HttpContext context, IGitLabApi gitLabApi) =>
{
    string? token = context.Request.Headers["Authorization"];
    if (string.IsNullOrEmpty(token))
    {
        return Results.BadRequest("Authorization token is required");
    }

    if (!token.StartsWith("Bearer "))
    {
        token = $"Bearer {token}";
    }

    var proj = await gitLabApi.GetProjectByIdAsync(id, token);

    return Results.Ok(proj);
});

app.MapDelete("/removeuser/{username}", async (string username, HttpContext context, IGitLabApi gitLabApi) =>
{
    var response = context.Response;
    //response.ContentType = "text/plain";
    response.ContentType = "text/event-stream";
    response.StatusCode = StatusCodes.Status200OK;

    response.Headers["Cache-Control"] = "no-cache";
    response.Headers["Connection"] = "keep-alive";
    response.Headers["Transfer-Encoding"] = "chunked";

    var writer = new StreamWriter(response.Body);
    await writer.WriteLineAsync($"Starting removal process for user: {username}...");
    await writer.FlushAsync();

    string? token = context.Request.Headers["Authorization"];
    if (string.IsNullOrEmpty(token))
    {
        await writer.WriteLineAsync("❌ Error: Authorization token is required.");
        await writer.FlushAsync();
        return;
    }

    if (!token.StartsWith("Bearer "))
    {
        token = $"Bearer {token}";
    }

    // Get user details from GitLab
    var users = await gitLabApi.GetUserByUserNameAsync(username);
    if (users == null || users.Count == 0)
    {
        await writer.WriteLineAsync($"❌ Error: User '{username}' not found.");
        await writer.FlushAsync();
        return;
    }

    var user = users.First();
    int userId = user.Id;
    await writer.WriteLineAsync($"✅ Found user: {user.Username} (ID: {userId})");
    await writer.FlushAsync();

    // Paginate through projects (100 per page)
    int page = 1;
    int perPage = 500;
    bool userRemoved = false;

    while (true)
    {
        var projects = await gitLabApi.GetOwnedProjectsAsync(page, perPage, token);
        if (projects == null || projects.Count == 0)
        {
            break;
        }

        foreach (var project in projects)
        {
            await writer.WriteLineAsync($"🔍 Checking project: {project.Name} (ID: {project.Id})...");
            await writer.FlushAsync();

            try
            {
                await gitLabApi.RemoveUserFromProjectAsync(project.Id, userId, token);
                userRemoved = true;
                await writer.WriteLineAsync($"✅ Successfully removed {username} from {project.Name}.");
            }
            catch (ApiException ex) // Handles GitLab API errors
            {
                await writer.WriteLineAsync($"❌ Failed to remove {username} from {project.Name}: {ex.Message}");
            }
            await writer.FlushAsync();
        }

        page++; // Move to the next page
    }

    if (userRemoved)
    {
        await writer.WriteLineAsync($"🎉 Done: User '{username}' has been removed from all projects.");
    }
    else
    {
        await writer.WriteLineAsync($"ℹ️ User '{username}' was not found in any projects.");
    }

    await writer.FlushAsync();
});

app.MapGet("/stream", async (HttpResponse response) =>
{
    response.ContentType = "text/event-stream";
    response.StatusCode = StatusCodes.Status200OK;
    response.Headers["Cache-Control"] = "no-cache";
    response.Headers["Connection"] = "keep-alive";
    response.Headers["Transfer-Encoding"] = "chunked";

    for (int i = 0; i < 10; i++)
    {
        await response.WriteAsync($"data: Event {i + 1}\n\n");
        await response.Body.FlushAsync();
        await Task.Delay(1000); // Simulate delay between events
    }
});

app.MapGet("/", () => "Hello World!");
app.Run();
