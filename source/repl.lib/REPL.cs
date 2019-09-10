﻿using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;

namespace nutility
{
  public class InputReplLevel<T> where T : class, new()
  {
    public InputReplLevel(string id)
    {
      ID = id;
      InputClass = typeof(T);
      ClassChilds = new Dictionary<string, Type>();
      Instances = new Dictionary<string, T>();
    }

    public string ID;
    public Type InputClass;
    public Dictionary<string, Type> ClassChilds;
    public Dictionary<string, T> Instances;
  }

  public class REPL<T> where T : class, new()
  {
    public TextWriter Writer { get; set; }
    public TextReader Reader { get; set; }

    /* Spec cases for units and whole interaction:
 dir = list current input class ID/Name
 _   = list current input class Usage
 dir = list current input child classes
 _   = add/create new input instance of the current input class or of the given/explicitly-qualified path/level input class
 _   = clone input instance of the current input instance or of the given/explicitly-qualified path/level input instance
 dir = list current input child instances
 _   = change to or make-current one of the childs (class or instance)
 _   = change to or make-current the parent of the current class or instance
 (args1) = process args on current class or instance
 (args2) = process args on class or instance of a given/explicit path/level
 record, store(script) and replay
*/

    public void Loop(InputReplLevel<T> current_level)
    {
      #region Input validation
      if (current_level == null)
      {
        throw new ArgumentNullException($"{nameof(current_level)}",$"{nameof(current_level)} cannot be null.");
      }
      if (Reader == null)
      {
        Reader = Console.In;
      }
      if (Writer == null)
      {
        Writer = Console.Out;
      }
      #endregion

      //var current_path = new Stack<string>();
      //current_path.Push(current_repl.Value.Name);

      do
      {
        Writer.WriteLine();
        Writer.Write($"{System.Reflection.MethodInfo.GetCurrentMethod().Name}: ");
        Writer.Write("> ");

        //Writer.WriteLine();
        //Writer.Write($"{path(current_path)}: ");

        var input = Reader.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(input)) break;
        var args = nutility.SystemArgumentParser.Parse(input);
        var opts = new nutility.Switch(args);
        if (args?.Any() == false || opts?.Any() == false) continue;
        if (input.StartsWith("?"))
        {
          ShowCurrentLevel(input, current_level);
        }
        else if (opts.IndexedArguments?.Count == 2 && opts.IndexedArguments?.First() == "new" && !string.IsNullOrWhiteSpace(opts.IndexedArguments[1]))
        {
          @new(opts.IndexedArguments[1], current_level);
        }
        else if (opts.IndexedArguments?.Count == 2 && opts.IndexedArguments?.First() == "delete" && !string.IsNullOrWhiteSpace(opts.IndexedArguments[1]))
        {
          delete(opts.IndexedArguments[1], current_level);
        }
        else if (opts.IndexedArguments?.Count == 1 && opts.IndexedArguments?.First() == "cls")
        {
          clear();
        }
        else
        {
          var childInputInstanceKey = args.Any() && current_level.Instances.ContainsKey(args.First()) ? args.First() : null;
          var childInputClassKey = args.Any() && current_level.ClassChilds.ContainsKey(args.First()) ? args.First() : null;
          var target_args = args.SkipWhile((arg, index) => index < 1).ToArray();
          if (childInputInstanceKey != null)
          {
            if (target_args?.Any() == true)
            {
              nutility.Switch.AsType(target_args, current_level.Instances[childInputInstanceKey]);
            }
            else
            {
              //TODO change current
            }
          }
          else if (childInputClassKey != null)
          {
            if (target_args?.Any() == true)
            {
              nutility.Switch.AsType(target_args, current_level.ClassChilds[childInputClassKey]);
            }
            else
            {
              //TODO change current
            }
          }
          else
          {
            nutility.Switch.AsType<T>(args);
          }
        }
      } while (true);
    }

    private string path(Stack<string> stack)
    {
      var result = "";
      if (stack.Count > 0)
      {
        result = $"{stack.ToArray().Reverse().Aggregate(new StringBuilder(), (w, n) => w.AppendFormat("/{0}", n))}";
      }
      return result;
    }

    private void @new(string key, InputReplLevel<T> current_level)
    {
      if (current_level.Instances.ContainsKey(key))
      {
        Writer.WriteLine($"{key} already identifies an existing instance.");
      }
      else
      {
        current_level.Instances.Add(key, new T());
        Writer.WriteLine($"\t{key} ({current_level.Instances[key].GetType().FullName})");
      }
    }
    private void delete(string key, InputReplLevel<T> current_level)
    {
      if (current_level.Instances.ContainsKey(key))
      {
        var instance = current_level.Instances[key];
        var classname = instance?.GetType().FullName;
        IDisposable disposable = instance as IDisposable;
        if (disposable != null)
        {
          Writer.WriteLine($"\t{key} ({classname}) disposing...");
          disposable.Dispose();
          Writer.WriteLine($"\t{key} ({classname}) disposed.");
        }
        current_level.Instances.Remove(key);
        Writer.WriteLine($"\t{key} ({classname}) removed.");
      }
      else
      {
        Writer.WriteLine($"{key} not found among existing instances.");
      }
    }
    private void ShowCurrentLevel(string input, InputReplLevel<T> current_level)
    {
      Writer.WriteLine($"Current input class: {current_level.InputClass.FullName}");
      if (input.Contains("??"))
      {
        Writer.WriteLine($"Working with {current_level.InputClass.FullName}:");
        ShowUsage(current_level.InputClass);
      }
      Writer.WriteLine($"\r\nCurrent input child classes ({current_level.ClassChilds.Count}):");
      foreach (var id in current_level.ClassChilds.Keys)
      {
        Writer.WriteLine($"\t{id} ({current_level.ClassChilds[id].FullName})");
        if (input.Contains("???"))
        {
          ShowUsage(current_level.ClassChilds[id]);
        }
      }
      Writer.WriteLine($"\r\nCurrent input instances ({current_level.Instances.Count}):");
      foreach (var id in current_level.Instances.Keys)
      {
        Writer.WriteLine($"\t{id} ({current_level.Instances[id].GetType().FullName})");
      }
      Writer.WriteLine("\r\nGeneral commands: ? ?? ??? new delete cls");
      if (input == "??")
      {
        Writer.WriteLine("\t? ?? (this help)");
        Writer.WriteLine("\tnew <id> (new instance of current input class)");
        Writer.WriteLine("\tdelete <id> (remove an existing instance of current input class)");
        Writer.WriteLine("\tcls (clear console)");
      }
    }

    private void ShowUsage(Type type)
    {
      var writer = new System.IO.StringWriter();
      nutility.Switch.ShowUsage(type, writer);
      var reader = new System.IO.StringReader($"{writer}");
      do
      {
        var line = reader.ReadLine();
        if (line == null) break;
        Writer.WriteLine($"\t{line}");
      } while (true);
    }
    private void clear() => Console.Clear();
  }
}