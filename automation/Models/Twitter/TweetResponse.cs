using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Automation.Models.Twitter;

public class TweetResponse
{
    [JsonPropertyName("data")]
    public List<Tweet>? Data { get; set; }

    [JsonPropertyName("meta")]
    public Meta? Meta { get; set; }
}



