using NJsonSchema.Generation;

namespace JustAnotherListAPI.Common.Generator
{
  public class PublicSchemaNameGenerator : ISchemaNameGenerator
  {
      public string Generate(Type type)
      {
        var postfix = type.Name.ToLower().IndexOf("dto");

        if (postfix >= 0)
        {
          return type.Name.Remove(postfix);
        }

        return type.Name;
      }
  }
}