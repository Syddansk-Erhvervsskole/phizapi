using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using phizapi.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Net.WebSockets;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy
            .AllowAnyOrigin()    // or use .WithOrigins("http://localhost:5500") for safety
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var token = context.Request.Cookies["access_token"];
                if (!string.IsNullOrEmpty(token))
                    context.Token = token;
                return Task.CompletedTask;
            }
        };
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
        };
    });

// Add services to the container.
builder.Services.AddAuthorization();
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SupportNonNullableReferenceTypes();
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "phizapi", Version = "v1" });

    // Add JWT Authentication
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter: Bearer {your token}"
    });

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
            Array.Empty<string>()
        }
    });
});

builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddSingleton<EncryptionService>();
builder.Services.AddSingleton<FtpService>();
builder.Services.AddSingleton<MongoDBService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseDeveloperExceptionPage(); // Only use in dev if you want
}

app.UseWebSockets();

app.Map("/ws", builder =>
{
    builder.Run(async context =>
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        // Get token from query string
        var token = context.Request.Query["token"].ToString();
        if (string.IsNullOrEmpty(token))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        // Verify token
        if (!VerifyJwtToken(token, context.RequestServices))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        using var clientSocket = await context.WebSockets.AcceptWebSocketAsync();

        // Connect to Python WS server
        using var serverSocket = new ClientWebSocket();
        await serverSocket.ConnectAsync(new Uri("ws://10.130.56.21:3000"), CancellationToken.None);

        var buffer = new byte[4096];

        var relayClientToServer = Task.Run(async () =>
        {
            while (clientSocket.State == WebSocketState.Open)
            {
                var result = await clientSocket.ReceiveAsync(buffer, CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                await serverSocket.SendAsync(buffer.AsMemory(0, result.Count),
                    result.MessageType, result.EndOfMessage, CancellationToken.None);
            }
        });

        var relayServerToClient = Task.Run(async () =>
        {
            while (serverSocket.State == WebSocketState.Open)
            {
                var result = await serverSocket.ReceiveAsync(buffer, CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                await clientSocket.SendAsync(buffer.AsMemory(0, result.Count),
                    result.MessageType, result.EndOfMessage, CancellationToken.None);
            }
        });

        await Task.WhenAny(relayClientToServer, relayServerToClient);
    });
});

bool VerifyJwtToken(string token, IServiceProvider services)
{
    var config = services.GetRequiredService<IConfiguration>();
    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]));

    var tokenHandler = new JwtSecurityTokenHandler();
    try
    {
        tokenHandler.ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ValidateIssuer = true,
            ValidIssuer = config["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = config["Jwt:Audience"],
            ClockSkew = TimeSpan.Zero
        }, out SecurityToken validatedToken);

        return true;
    }
    catch
    {
        return false;
    }
}

//app.UseHttpsRedirection();

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
