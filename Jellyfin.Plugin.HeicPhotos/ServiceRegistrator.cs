using MediaBrowser.Controller;
using MediaBrowser.Controller.Drawing;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.HeicPhotos;

/// <summary>
/// Puts the HEIC-aware encoder in front of the server's own one. Plugins register after the
/// server, so the server's registration is there to wrap.
/// </summary>
public class ServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<HeicConverter>();
        var original = serviceCollection.LastOrDefault(d => d.ServiceType == typeof(IImageEncoder));
        if (original is null)
        {
            return;
        }

        serviceCollection.Remove(original);
        serviceCollection.AddSingleton<IImageEncoder>(sp => new HeicImageEncoder(Make(sp, original), sp.GetRequiredService<HeicConverter>()));
    }

    private static IImageEncoder Make(IServiceProvider sp, ServiceDescriptor d)
        => (IImageEncoder)(d.ImplementationInstance
            ?? d.ImplementationFactory?.Invoke(sp)
            ?? ActivatorUtilities.CreateInstance(sp, d.ImplementationType!));
}
