using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SharpYaml.Serialization;

namespace QuickMCP.Helpers
{
   

    #region Interface and Helper Classes

    /// <summary>
    /// Interface for the JSON Schema visitor pattern
    /// </summary>
    public delegate JObject SchemaVisitor(JObject schema);

    /// <summary>
    /// Interface for the reference object visitor pattern
    /// </summary>
    public delegate JToken RefObjectVisitor(JObject refObject);

    /// <summary>
    /// Lightweight OAS document top-level fields
    /// </summary>
    public class OpenAPI3
    {
        public string Openapi { get; set; }
        public JObject Info { get; set; }
        public JObject Paths { get; set; }
        public JObject Components { get; set; }
        public JObject Tags { get; set; }
    }

    /// <summary>
    /// Options for the converter instantiation
    /// </summary>
    public class ConverterOptions
    {
        /// <summary>
        /// If true, log conversion transformations to stderr
        /// </summary>
        public bool Verbose { get; set; }

        /// <summary>
        /// If true, remove id values in schema examples, to bypass Spectral issue 2081
        /// </summary>
        public bool DeleteExampleWithId { get; set; }

        /// <summary>
        /// If true, replace a $ref object that has siblings into an allOf
        /// </summary>
        public bool AllOfTransform { get; set; }

        /// <summary>
        /// The authorizationUrl for openIdConnect -> oauth2 transformation
        /// </summary>
        public string AuthorizationUrl { get; set; }

        /// <summary>
        /// The tokenUrl for openIdConnect -> oauth2 transformation
        /// </summary>
        public string TokenUrl { get; set; }

        /// <summary>
        /// If true, convert openIdConnect security scheme to oauth2
        /// </summary>
        public bool ConvertOpenIdConnectToOAuth2 { get; set; }

        /// <summary>
        /// Name of YAML/JSON file with scope descriptions
        /// </summary>
        public string ScopeDescriptionFile { get; set; }

        /// <summary>
        /// Use this option to preserve the conversion and not delete comments
        /// </summary>
        public bool ConvertSchemaComments { get; set; }
        
        /// <summary>
        /// If true, convert numeric values to booleans for boolean schema properties
        /// </summary>
        public bool FixNumericBooleans { get; set; } = true;
    }

    #endregion

    public class Converter
    {
        private JObject openapi30;
        private bool verbose = false;
        private bool deleteExampleWithId = false;
        private bool allOfTransform = false;
        private string authorizationUrl;
        private string tokenUrl;
        private JObject scopeDescriptions;
        private bool convertSchemaComments = false;
        private int returnCode = 0;
        private bool convertOpenIdConnectToOAuth2;
        private bool fixNumericBooleans = true;

        /// <summary>
        /// HTTP methods
        /// </summary>
        private static readonly string[] HTTP_METHODS = new string[] { "delete", "get", "head", "options", "patch", "post", "put", "trace" };

        /// <summary>
        /// Construct a new Converter
        /// </summary>
        /// <param name="openapiDocument">OpenAPI document as JObject</param>
        /// <param name="options">Converter options</param>
        /// <exception cref="Exception">If the scopeDescriptionFile cannot be read or parsed</exception>
        public Converter(JObject openapiDocument, ConverterOptions? options = null)
        {
            options = options ?? new ConverterOptions();
            
            this.openapi30 = DeepClone(openapiDocument);
            this.verbose = options.Verbose;
            this.deleteExampleWithId = options.DeleteExampleWithId;
            this.allOfTransform = options.AllOfTransform;
            this.authorizationUrl = options.AuthorizationUrl ?? "https://www.example.com/oauth2/authorize";
            this.tokenUrl = options.TokenUrl ?? "https://www.example.com/oauth2/token";
            this.convertOpenIdConnectToOAuth2 = options.ConvertOpenIdConnectToOAuth2 || !string.IsNullOrEmpty(options.ScopeDescriptionFile);
            this.fixNumericBooleans = options.FixNumericBooleans;
            
            if (this.convertOpenIdConnectToOAuth2 && !string.IsNullOrEmpty(options.ScopeDescriptionFile))
            {
                LoadScopeDescriptions(options.ScopeDescriptionFile);
            }
            
            this.convertSchemaComments = options.ConvertSchemaComments;
        }

        /// <summary>
        /// Load the scopes.yaml file and save in scopeDescriptions
        /// </summary>
        /// <param name="scopeDescriptionFile">Path to the file</param>
        /// <exception cref="Exception">If the file cannot be read or parsed</exception>
        private void LoadScopeDescriptions(string scopeDescriptionFile)
        {
            if (string.IsNullOrEmpty(scopeDescriptionFile))
                return;

            try
            {
                var deserializer = new Serializer();
                var yamlObject = deserializer.Deserialize(File.ReadAllText(scopeDescriptionFile));
                this.scopeDescriptions = JObject.FromObject(yamlObject);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to load scope descriptions: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Log a message to console if verbose is true
        /// </summary>
        private void Log(string message, params object[] args)
        {
            if (this.verbose)
            {
                Warn(message, args);
            }
        }

        /// <summary>
        /// Log a warning message to console
        /// </summary>
        private void Warn(string message, params object[] args)
        {
            if (!message.StartsWith("Warning"))
            {
                message = $"Warning: {message}";
            }
            Console.Error.WriteLine(message, args);
        }

        /// <summary>
        /// Log an error message to console
        /// </summary>
        private void Error(string message, params object[] args)
        {
            if (!message.StartsWith("Error"))
            {
                message = $"Error: {message}";
            }
            this.returnCode++;
            Console.Error.WriteLine(message, args);
        }

        /// <summary>
        /// Convert the OpenAPI document to 3.0
        /// </summary>
        /// <returns>The converted document. The input is not modified.</returns>
        /// <exception cref="Exception">If conversion fails</exception>
        public JObject Convert()
        {
            Log("Converting from OpenAPI 3.1 to 3.0");
            
            this.openapi30["openapi"] = "3.0.3";
            RemoveLicenseIdentifier();
            ConvertSchemaRef();
            SimplifyNonSchemaRef();
            
            if (this.convertOpenIdConnectToOAuth2)
            {
                ConvertOpenIdConnectSecuritySchemesToOAuth2();
            }
            
            ConvertJsonSchemaExamples();
            ConvertJsonSchemaContentEncoding();
            ConvertJsonSchemaContentMediaType();
            ConvertConstToEnum();
            ConvertNullableTypeArray();
            RemoveWebhooksObject();
            RemoveUnsupportedSchemaKeywords();
            
            if (this.convertSchemaComments)
            {
                RenameSchemaComment();
            }
            else
            {
               DeleteSchemaComment();
            }
            
            if (this.fixNumericBooleans)
            {
                FixNumericBooleanValues();
            }
            
            if (this.returnCode > 0)
            {
                throw new Exception("Cannot down convert this OpenAPI definition.");
            }
            
            return this.openapi30;
        }

        /// <summary>
        /// Convert JSON Schema examples to example
        /// </summary>
        private void ConvertJsonSchemaExamples()
        {
            SchemaVisitor schemaVisitor = null; // Initialize with null first

            // Define the visitor
            schemaVisitor = (schema) =>
            {
                foreach (var prop in schema.Properties().ToList())
                {
                    var key = prop.Name;
                    var value = prop.Value;

                    if (value != null && value.Type == JTokenType.Object)
                    {
                        if (key == "examples")
                        {
                            JArray examples = schema["examples"] as JArray;
                            if (examples != null && examples.Count > 0)
                            {
                                schema.Remove("examples");
                                var first = examples[0];
                                
                                if (this.deleteExampleWithId && 
                                    first != null && 
                                    first.Type == JTokenType.Object && 
                                    first["id"] != null)
                                {
                                    Log($"Deleted schema example with `id` property:\n{Json(examples)}");
                                }
                                else
                                {
                                    schema["example"] = first;
                                    Log($"Replaces examples with examples[0]. Old examples:\n{Json(examples)}");
                                }
                            }
                        }
                        else
                        {
                            schema[key] = WalkObject(value, schemaVisitor);
                        }
                    }
                }
                
                return schema;
            };

            VisitSchemaObjects(this.openapi30, schemaVisitor);
        }

        /// <summary>
        /// Walk through nested schema objects
        /// </summary>
        private JToken WalkNestedSchemaObjects(JToken token, SchemaVisitor schemaVisitor)
        {
            if (token is JObject obj)
            {
                foreach (var prop in obj.Properties().ToList())
                {
                    var key = prop.Name;
                    var value = prop.Value;

                    if (value != null && (value.Type == JTokenType.Object || value.Type == JTokenType.Array))
                    {
                        obj[key] = WalkObject(value, schemaVisitor);
                    }
                }
            }
            
            return token;
        }

        /// <summary>
        /// Convert const to enum
        /// </summary>
        private void ConvertConstToEnum()
        {
            SchemaVisitor schemaVisitor = null; // Initialize with null first
            
            schemaVisitor = (schema) =>
            {
                if (schema["const"] != null)
                {
                    var constant = schema["const"];
                    schema.Remove("const");
                    schema["enum"] = new JArray { constant };
                    Log($"Converted const: {constant} to enum");
                }
                
                return WalkNestedSchemaObjects(schema, schemaVisitor) as JObject;
            };
            
            VisitSchemaObjects(this.openapi30, schemaVisitor);
        }

        /// <summary>
        /// Convert 2-element type arrays containing 'null' to string type and nullable: true
        /// </summary>
        private void ConvertNullableTypeArray()
        {
            SchemaVisitor schemaVisitor = null; // Initialize with null first
            
            schemaVisitor = (schema) =>
            {
                if (schema["type"] != null)
                {
                    var schemaType = schema["type"];
                    if (schemaType.Type == JTokenType.Array)
                    {
                        var typeArray = schemaType as JArray;
                        if (typeArray.Count == 2 && typeArray.Any(t => t.ToString() == "null"))
                        {
                            var nonNull = typeArray.FirstOrDefault(t => t.ToString() != "null");
                            schema["type"] = nonNull;
                            schema["nullable"] = true;
                            Log("Converted schema type array to nullable");
                        }
                    }
                }
                
                return WalkNestedSchemaObjects(schema, schemaVisitor) as JObject;
            };
            
            VisitSchemaObjects(this.openapi30, schemaVisitor);
        }

        /// <summary>
        /// Remove webhooks object
        /// </summary>
        private void RemoveWebhooksObject()
        {
            if (this.openapi30["webhooks"] != null)
            {
                Log("Deleted webhooks object");
                this.openapi30.Remove("webhooks");
            }
        }

        /// <summary>
        /// Remove unsupported schema keywords
        /// </summary>
        private void RemoveUnsupportedSchemaKeywords()
        {
            string[] keywordsToRemove = { "$id", "$schema", "unevaluatedProperties", "contentMediaType", "patternProperties", "propertyNames" };
            
            SchemaVisitor schemaVisitor = null; // Initialize with null first
            
            schemaVisitor = (schema) =>
            {
                foreach (var key in keywordsToRemove)
                {
                    if (schema[key] != null)
                    {
                        schema.Remove(key);
                        Log($"Removed unsupported schema keyword {key}");
                    }
                }
                
                return WalkNestedSchemaObjects(schema, schemaVisitor) as JObject;
            };
            
            VisitSchemaObjects(this.openapi30, schemaVisitor);
        }

        /// <summary>
        /// Rename schema $comment to x-comment
        /// </summary>
        private void RenameSchemaComment()
        {
            SchemaVisitor schemaVisitor = null; // Initialize with null first
            
            schemaVisitor = (schema) =>
            {
                if (schema["$comment"] != null)
                {
                    schema["x-comment"] = schema["$comment"];
                    schema.Remove("$comment");
                    Log("schema $comment renamed to x-comment");
                }
                
                return WalkNestedSchemaObjects(schema, schemaVisitor) as JObject;
            };
            
            VisitSchemaObjects(this.openapi30, schemaVisitor);
        }

        /// <summary>
        /// Delete schema $comment
        /// </summary>
        private void DeleteSchemaComment()
        {
            SchemaVisitor schemaVisitor = null; // Initialize with null first
            
            schemaVisitor = (schema) =>
            {
                if (schema["$comment"] != null)
                {
                    var comment = schema["$comment"];
                    schema.Remove("$comment");
                    Log($"schema $comment deleted: {comment}");
                }
                
                return WalkNestedSchemaObjects(schema, schemaVisitor) as JObject;
            };
            
            VisitSchemaObjects(this.openapi30, schemaVisitor);
        }

        /// <summary>
        /// Convert contentMediaType to format
        /// </summary>
        private void ConvertJsonSchemaContentMediaType()
        {
            SchemaVisitor schemaVisitor = null; // Initialize with null first
            
            schemaVisitor = (schema) =>
            {
                if (schema["type"] != null && 
                    schema["type"].ToString() == "string" && 
                    schema["contentMediaType"] != null && 
                    schema["contentMediaType"].ToString() == "application/octet-stream")
                {
                    if (schema["format"] != null)
                    {
                        if (schema["format"].ToString() == "binary")
                        {
                            Log("Deleted schema contentMediaType: application/octet-stream (leaving format: binary)");
                            schema.Remove("contentMediaType");
                        }
                        else
                        {
                            Error(
                                $"Unable to down-convert schema with contentMediaType: application/octet-stream to format: binary because the schema already has a format ({schema["format"]})"
                            );
                        }
                    }
                    else
                    {
                        schema.Remove("contentMediaType");
                        schema["format"] = "binary";
                        Log("Converted schema contentMediaType: application/octet-stream to format: binary");
                    }
                }
                
                return WalkNestedSchemaObjects(schema, schemaVisitor) as JObject;
            };
            
            VisitSchemaObjects(this.openapi30, schemaVisitor);
        }

        /// <summary>
        /// Convert contentEncoding to format
        /// </summary>
        private void ConvertJsonSchemaContentEncoding()
        {
            SchemaVisitor schemaVisitor = null; // Initialize with null first
            
            schemaVisitor = (schema) =>
            {
                if (schema["type"] != null && 
                    schema["type"].ToString() == "string" && 
                    schema["contentEncoding"] != null)
                {
                    if (schema["contentEncoding"].ToString() == "base64")
                    {
                        if (schema["format"] != null)
                        {
                            if (schema["format"].ToString() == "byte")
                            {
                                Log("Deleted schema contentEncoding: base64 (leaving format: byte)");
                                schema.Remove("contentEncoding");
                            }
                            else
                            {
                                Error(
                                    $"Unable to down-convert schema contentEncoding: base64 to format: byte because the schema already has a format ({schema["format"]})"
                                );
                            }
                        }
                        else
                        {
                            schema.Remove("contentEncoding");
                            schema["format"] = "byte";
                            Log("Converted schema: 'contentEncoding: base64' to 'format: byte'");
                        }
                    }
                    else
                    {
                        Error($"Unable to down-convert contentEncoding: {schema["contentEncoding"]}");
                    }
                }
                
                return WalkNestedSchemaObjects(schema, schemaVisitor) as JObject;
            };
            
            VisitSchemaObjects(this.openapi30, schemaVisitor);
        }

        /// <summary>
        /// Convert numeric values to boolean for properties that should be boolean
        /// </summary>
        private void FixNumericBooleanValues()
        {
            // List of common schema properties that should be boolean
            string[] booleanProperties = { "nullable", "required", "deprecated", "readOnly", "writeOnly", "uniqueItems", "exclusiveMaximum", "exclusiveMinimum" };
            
            SchemaVisitor schemaVisitor = null;
            
            schemaVisitor = (schema) =>
            {
                foreach (var propName in booleanProperties)
                {
                    if (schema[propName] != null && 
                        (schema[propName].Type == JTokenType.Float || 
                         schema[propName].Type == JTokenType.Integer || 
                         schema[propName].Type == JTokenType.String))
                    {
                        var originalValue = schema[propName];
                        bool boolValue;
                        
                        // Handle different types of values
                        if (schema[propName].Type == JTokenType.String)
                        {
                            var strValue = schema[propName].ToString();
                            
                            if (strValue == "0" || strValue == "0.0" || strValue.ToLower() == "false")
                                boolValue = false;
                            else if (strValue == "1" || strValue == "1.0" || strValue.ToLower() == "true")
                                boolValue = true;
                            else
                            {
                                // Try to convert from numeric string to boolean
                                if (double.TryParse(strValue, out double numValue))
                                    boolValue = numValue != 0;
                                else
                                    continue; // Skip if cannot convert
                            }
                        }
                        else // For numeric types
                        {
                            var numValue = schema[propName].Value<double>();
                            boolValue = numValue != 0;
                        }
                        
                        schema[propName] = boolValue;
                        Log($"Converted {propName} value from {originalValue} to {boolValue}");
                    }
                    
                    // Handle boolean properties inside arrays and objects
                    if (schema["properties"] != null && schema["properties"].Type == JTokenType.Object)
                    {
                        foreach (var prop in schema["properties"].ToObject<JObject>().Properties())
                        {
                            if (prop.Value.Type == JTokenType.Object)
                            {
                                foreach (var boolProp in booleanProperties)
                                {
                                    if (prop.Value[boolProp] != null && 
                                        (prop.Value[boolProp].Type == JTokenType.Float || 
                                         prop.Value[boolProp].Type == JTokenType.Integer || 
                                         prop.Value[boolProp].Type == JTokenType.String))
                                    {
                                        var originalValue = prop.Value[boolProp];
                                        
                                        if (prop.Value[boolProp].Type == JTokenType.String)
                                        {
                                            var strValue = prop.Value[boolProp].ToString();
                                            if (strValue == "0" || strValue == "0.0" || strValue.ToLower() == "false")
                                                prop.Value[boolProp] = false;
                                            else if (strValue == "1" || strValue == "1.0" || strValue.ToLower() == "true")
                                                prop.Value[boolProp] = true;
                                            else if (double.TryParse(strValue, out double numValue))
                                                prop.Value[boolProp] = numValue != 0;
                                        }
                                        else
                                        {
                                            var numValue = prop.Value[boolProp].Value<double>();
                                            prop.Value[boolProp] = numValue != 0;
                                        }
                                        
                                        Log($"Converted property {prop.Name}.{boolProp} value from {originalValue} to {prop.Value[boolProp]}");
                                    }
                                }
                            }
                        }
                    }
                }
                
                return WalkNestedSchemaObjects(schema, schemaVisitor) as JObject;
            };
            
            VisitSchemaObjects(this.openapi30, schemaVisitor);
        }

        /// <summary>
        /// Convert object to JSON string
        /// </summary>
        private string Json(object obj)
        {
            return JsonConvert.SerializeObject(obj, Formatting.Indented);
        }

        /// <summary>
        /// Convert openIdConnect security schemes to OAuth2
        /// </summary>
        private void ConvertOpenIdConnectSecuritySchemesToOAuth2()
        {
            JObject OAuth2Scopes(string schemeName)
            {
                var scopes = new JObject();
                var paths = this.openapi30["paths"] as JObject;
                
                if (paths == null)
                    return scopes;
                
                foreach (var pathProp in paths.Properties())
                {
                    var path = pathProp.Name;
                    var pathObj = pathProp.Value as JObject;
                    
                    // Filter to include only HTTP methods
                    var methods = pathObj.Properties()
                        .Where(p => HTTP_METHODS.Contains(p.Name))
                        .Select(p => p.Name);
                    
                    foreach (var method in methods)
                    {
                        var operation = pathObj[method] as JObject;
                        var security = operation?["security"] as JArray ?? new JArray();
                        
                        foreach (var sec in security)
                        {
                            var requirement = sec?[schemeName] as JArray;
                            if (requirement != null)
                            {
                                foreach (var scope in requirement)
                                {
                                    var scopeName = scope.ToString();
                                    if (!scopes.ContainsKey(scopeName))
                                    {
                                        var description = this.scopeDescriptions?[scopeName]?.ToString() ?? $"TODO: describe the '{scopeName}' scope";
                                        scopes[scopeName] = description;
                                    }
                                }
                            }
                        }
                    }
                }
                
                return scopes;
            }

            var schemes = this.openapi30["components"]?["securitySchemes"] as JObject;
            if (schemes == null)
                return;
            
            foreach (var schemeProp in schemes.Properties())
            {
                var schemeName = schemeProp.Name;
                var scheme = schemeProp.Value as JObject;
                
                if (scheme?["type"]?.ToString() == "openIdConnect")
                {
                    Log("Converting openIdConnect security scheme to oauth2/authorizationCode");
                    
                    scheme["type"] = "oauth2";
                    var openIdConnectUrl = scheme["openIdConnectUrl"].ToString();
                    scheme["description"] = $@"OAuth2 Authorization Code Flow. The client may
GET the OpenID Connect configuration JSON from `{openIdConnectUrl}`
to get the correct `authorizationUrl` and `tokenUrl`.";
                    
                    scheme.Remove("openIdConnectUrl");
                    
                    var scopes = OAuth2Scopes(schemeName);
                    scheme["flows"] = new JObject
                    {
                        ["authorizationCode"] = new JObject
                        {
                            ["authorizationUrl"] = this.authorizationUrl,
                            ["tokenUrl"] = this.tokenUrl,
                            ["scopes"] = scopes
                        }
                    };
                }
            }
        }

        /// <summary>
        /// Simplify reference objects that are not in schemas
        /// </summary>
        private void SimplifyNonSchemaRef()
        {
            VisitRefObjects(this.openapi30, (node) =>
            {
                if (node.Count == 1)
                {
                    return node;
                }
                else
                {
                    Log($"Down convert reference object to JSON Reference:\n{JsonConvert.SerializeObject(node, Formatting.Indented)}");
                    
                    var refValue = node["$ref"];
                    node.RemoveAll();
                    node["$ref"] = refValue;
                    
                    return node;
                }
            });
        }

        /// <summary>
        /// Remove license identifier
        /// </summary>
        private void RemoveLicenseIdentifier()
        {
            var license = this.openapi30["info"]?["license"] as JObject;
            if (license?["identifier"] != null)
            {
                Log($"Removed info.license.identifier: {license["identifier"]}");
                license.Remove("identifier");
            }
        }

        /// <summary>
        /// Convert schema reference
        /// </summary>
        private void ConvertSchemaRef()
        {
            if (!this.allOfTransform)
                return;

            JObject SimplifyRefObjectsInSchemas(JObject schema)
            {
                return VisitRefObjects(schema, (node) =>
                {
                    if (node.Count == 1)
                    {
                        return node;
                    }
                    else
                    {
                        Log($"Converting JSON Schema $ref {Json(node)} to allOf: [ $ref ]");
                        
                        var refValue = node["$ref"].ToString();
                        node["allOf"] = new JArray { new JObject { ["$ref"] = refValue } };
                        node.Remove("$ref");
                        
                        return node;
                    }
                }) as JObject;
            }

            // Create a visitor that will apply the SimplifyRefObjectsInSchemas function
            SchemaVisitor schemaVisitor = (schema) => SimplifyRefObjectsInSchemas(schema);

            VisitSchemaObjects(this.openapi30, schemaVisitor);
        }

        /// <summary>
        /// Create a deep clone of a JObject
        /// </summary>
        public static JObject DeepClone(JObject obj)
        {
            return JObject.Parse(JsonConvert.SerializeObject(obj));
        }

        #region Schema and Ref Visitor Methods

        /// <summary>
        /// Walk an object and apply the visitor function to schema objects
        /// </summary>
        private JToken WalkObject(JToken token, SchemaVisitor visitor)
        {
            if (visitor == null)
            {
                throw new ArgumentNullException(nameof(visitor), "Schema visitor cannot be null");
            }
            
            if (token is JObject obj)
            {
                // If this is a schema object, visit it
                if (IsSchemaObject(obj))
                {
                    return visitor(obj);
                }
                
                // Otherwise, walk through all properties
                foreach (var prop in obj.Properties().ToList())
                {
                    obj[prop.Name] = WalkObject(prop.Value, visitor);
                }
                
                return obj;
            }
            else if (token is JArray array)
            {
                for (int i = 0; i < array.Count; i++)
                {
                    array[i] = WalkObject(array[i], visitor);
                }
                
                return array;
            }
            
            return token;
        }

        /// <summary>
        /// Determine if an object is a JSON Schema
        /// </summary>
        private bool IsSchemaObject(JObject obj)
        {
            // Simple heuristic: if it has type, properties, items, or allOf, it's probably a schema
            return obj["type"] != null || obj["properties"] != null || obj["items"] != null || obj["allOf"] != null;
        }

        /// <summary>
        /// Visit all schema objects in the OpenAPI document
        /// </summary>
        private void VisitSchemaObjects(JToken token, SchemaVisitor visitor)
        {
            if (visitor == null)
            {
                throw new ArgumentNullException(nameof(visitor), "Schema visitor cannot be null");
            }
            WalkObject(token, visitor);
        }

        /// <summary>
        /// Walk an object and apply the visitor function to reference objects
        /// </summary>
        private JToken VisitRefObjects(JToken token, RefObjectVisitor visitor)
        {
            if (visitor == null)
            {
                throw new ArgumentNullException(nameof(visitor), "Reference visitor cannot be null");
            }
            
            if (token is JObject obj)
            {
                // If this is a reference object, visit it
                if (obj["$ref"] != null)
                {
                    return visitor(obj);
                }
                
                // Otherwise, walk through all properties
                foreach (var prop in obj.Properties().ToList())
                {
                    obj[prop.Name] = VisitRefObjects(prop.Value, visitor);
                }
                
                return obj;
            }
            else if (token is JArray array)
            {
                for (int i = 0; i < array.Count; i++)
                {
                    array[i] = VisitRefObjects(array[i], visitor);
                }
                
                return array;
            }
            
            return token;
        }

        #endregion
    }
}