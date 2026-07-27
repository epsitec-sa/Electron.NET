namespace ElectronNET.Build
{
    using System;
    using System.IO;
    using System.Text.Json.Nodes;
    using Microsoft.Build.Framework;
    using Microsoft.Build.Utilities;

    public class RemoveJsonPropertyTask : Task
    {
        [Required]
        public string File { get; set; }

        [Required]
        public string PropertyName { get; set; }

        public override bool Execute()
        {
            try
            {
                if (!System.IO.File.Exists(this.File))
                {
                    return true;
                }

                string content = System.IO.File.ReadAllText(this.File);
                var node = JsonNode.Parse(content) as JsonObject;
                if (node == null || !node.ContainsKey(this.PropertyName))
                {
                    return true;
                }

                node.Remove(this.PropertyName);

                string updatedContent = node.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                System.IO.File.WriteAllText(this.File, updatedContent);

                return true;
            }
            catch (Exception ex)
            {
                this.Log.LogErrorFromException(ex);
                return false;
            }
        }
    }
}
