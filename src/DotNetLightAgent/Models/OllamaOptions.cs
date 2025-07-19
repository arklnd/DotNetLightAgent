namespace DotNetLightAgent.Models;

public class OllamaOptions
{
    public const string SectionName = "Ollama";
    
    public string ModelId { get; set; } = "qwen3";
    public string Endpoint { get; set; } = "http://localhost:11434";
    
    /// <summary>
    /// Validates the configuration and throws an exception if invalid
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ModelId))
        {
            throw new InvalidOperationException("Ollama ModelId cannot be null or empty");
        }
        
        if (string.IsNullOrWhiteSpace(Endpoint))
        {
            throw new InvalidOperationException("Ollama Endpoint cannot be null or empty");
        }
        
        if (!Uri.TryCreate(Endpoint, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException($"Ollama Endpoint '{Endpoint}' is not a valid URI");
        }
    }
}
