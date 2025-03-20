using System;
using System.Collections.Generic;
using Newtonsoft.Json;
namespace System.Runtime.CompilerServices
{
    // Dummy class to make the compiler happy :')
    // Enables using some C# 9 features like record types
    internal static class IsExternalInit { }
}


namespace Conjurer.Api
{
    // Base message type for all WebSocket messages
    public record ConjurerApiEvent
    {
        public string @event { get; init; }
        public object data { get; init; }

        public ConjurerApiEvent(string @event, object data)
        {
            this.@event = @event;
            this.data = data;
        }
    }

    // Command message for outgoing requests
    public record CommandMessage : ConjurerApiEvent
    {
        public CommandMessage(string commandName, object[] parameters = null)
            : base("command", new CommandData(commandName, parameters ?? Array.Empty<object>()))
        {
        }
    }

    public record CommandData
    {
        public string command { get; init; }
        public object[] @params { get; init; }

        public CommandData(string command, object[] parameters)
        {
            this.command = command;
            this.@params = parameters;
        }
    }

    // Mode selection message`
    public record SelectModeMessage : ConjurerApiEvent
    {
        public SelectModeMessage(string modeName)
            : base("select_mode", new { name = modeName })
        {
        }
    }

    // State update message and related types
    public record StateUpdateData
    {
        public string browser_tab_state { get; init; }
        public string[] modes_available { get; init; }
        public ModeData current_mode { get; init; }

        public StateUpdateData(string browser_tab_state, string[] modes_available, ModeData current_mode)
        {
            this.browser_tab_state = browser_tab_state;
            this.modes_available = modes_available;
            this.current_mode = current_mode;
        }
    }

    public record ModeData
    {
        public string name { get; init; }
        public CommandDefinition[] commands { get; init; }
        public string[] patterns_available { get; init; }
        public string[] effects_available { get; init; }
        public PatternData? current_pattern { get; init; }

        public ModeData(string name, CommandDefinition[] commands)
        {
            this.name = name;
            this.commands = commands;
        }
    }

    public record CommandDefinition
    {
        public string name { get; init; }
        public ParamDefinition[] @params { get; init; }

        public CommandDefinition(string name, ParamDefinition[] @params)
        {
            this.name = name;
            this.@params = @params;
        }
    }

    public record ParamDefinition
    {
        public string name { get; init; }
        public string? value { get; init; }

        public ParamDefinition(string name)
        {
            this.name = name;
        }
    }

    public record PatternData
    {
        public string name { get; init; }
        public Dictionary<string, PatternParameter> @params { get; init; }

        public PatternData(string name, Dictionary<string, PatternParameter> @params)
        {
            this.name = name;
            this.@params = @params;
        }
    }

    public record PatternParameter
    {
        public string name { get; init; }
        public float value { get; init; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public float? min { get; init; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public float? max { get; init; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public float? step { get; init; }

        public PatternParameter(string name, float value)
        {
            this.name = name;
            this.value = value;
        }
    }

    // Extension method to make creating state update messages easier
    public static class WebSocketMessageExtensions
    {
        public static ConjurerApiEvent CreateStateUpdateMessage(this StateUpdateData data) =>
            new ConjurerApiEvent("conjurer_state_update", data);
    }
}