using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Sgcf.Application.Common.Behaviors;
using Sgcf.Application.Cotacoes;
using Sgcf.Application.Cotacoes.Conversores;

namespace Sgcf.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        // Registrar todos os conversores de modalidade — inclusive os stubs.
        // A ordem de registro não importa: o handler indexa por ModalidadeContrato.
        // Stubs lançam NotImplementedException ao ser invocados, não ao ser registrados.
        services.AddScoped<IConversorModalidade, ConversorFinimp>();
        services.AddScoped<IConversorModalidade, ConversorRefinimp>();
        services.AddScoped<IConversorModalidade, ConversorLei4131>();
        services.AddScoped<IConversorModalidade, ConversorNce>();
        services.AddScoped<IConversorModalidade, ConversorCapitalDeGiro>();
        services.AddScoped<IConversorModalidade, ConversorFgi>();

        return services;
    }
}
