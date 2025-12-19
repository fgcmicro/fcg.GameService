using Amazon.XRay.Recorder.Core;
using Amazon.XRay.Recorder.Handlers.AspNetCore;
using Amazon.XRay.Recorder.Handlers.System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace fcg.GameService.Infrastructure.Configurations;

public static class XRaySettings
{
    public static IServiceCollection AddXRaySettings(this IServiceCollection services, IConfiguration configuration)
    {
        // Verifica se X-Ray está desabilitado (útil para desenvolvimento ou AWS Academy sem acesso)
        var tracingDisabled = configuration.GetValue<bool>("XRay:TracingDisabled", false);
        
        if (tracingDisabled)
        {
            Console.WriteLine("X-Ray está desabilitado via configuração.");
            return services;
        }
        
        try
        {
            // Configura o nome do serviço para X-Ray
            var serviceName = configuration["XRay:ServiceName"] ?? "fcg-games-service";
            
            // Inicializa o X-Ray Recorder se ainda não foi inicializado
            if (AWSXRayRecorder.Instance == null)
            {
                AWSXRayRecorder.InitializeInstance(configuration);
            }
            
            // Configura estratégia para quando o contexto está faltando
            // LOG_ERROR é mais tolerante - apenas loga erros sem quebrar a aplicação
            if (AWSXRayRecorder.Instance != null)
            {
                AWSXRayRecorder.Instance.ContextMissingStrategy = 
                    Amazon.XRay.Recorder.Core.Strategies.ContextMissingStrategy.LOG_ERROR;
            }
            
            Console.WriteLine($"X-Ray configurado para serviço: {serviceName}");
        }
        catch (Exception ex)
        {
            // Log erro mas não quebra a aplicação - importante para AWS Academy Lab
            Console.WriteLine($"⚠️ X-Ray não pôde ser configurado: {ex.Message}");
            Console.WriteLine("A aplicação continuará funcionando sem X-Ray.");
        }
        
        return services;
    }
}
