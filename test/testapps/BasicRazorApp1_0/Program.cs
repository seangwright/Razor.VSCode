using System;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Foo.Bar.Baz;

namespace BasicRazorApp1_0
{
    [HtmlTargetElement("environment")]
    public class FooTagHelper : TagHelper
    {

    }
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("This application is not intended to be run. It exists only for VS Code Razor Extension functional testing.");
        }
    }
}
