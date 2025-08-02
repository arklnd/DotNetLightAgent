using System.ComponentModel;
using Microsoft.SemanticKernel;
using DotNetLightAgent.Models;


namespace DotNetLightAgent.Plugins;

public class LightsPlugin
{
    // Mock data for the lights
    private readonly List<LightModel> lights =
    [
        new LightModel { Id = 1, Name = "Table Lamp", IsOn = false },
        new LightModel { Id = 2, Name = "Porch light", IsOn = false },
        new LightModel { Id = 3, Name = "Chandelier", IsOn = true }
    ];

    [KernelFunction("get_lights")]
    [Description("Gets a list of lights and their current state")]
    public List<LightModel> GetLights()
    {
        return lights;
    }

    [KernelFunction("change_state")]
    [Description("Changes the state of the light")]
    public LightModel? ChangeState(int id, bool isOn)
    {
        var light = lights.FirstOrDefault(light => light.Id == id);

        if (light == null)
        {
            return null;
        }

        // Update the light with the new state
        light.IsOn = isOn;

        return light;
    }
}
