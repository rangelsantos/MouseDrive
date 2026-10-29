using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace MouseDrive;

// Profiles are JSON files in <exe>\Profiles; _active.txt names the one in use.
public static class ProfileManager
{
	private static readonly JsonSerializerOptions Json = new JsonSerializerOptions
	{
		WriteIndented = true
	};

	public static string DirPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Profiles");

	private static string ActivePath => Path.Combine(DirPath, "_active.txt");

	private static string FileFor(string name)
	{
		return Path.Combine(DirPath, Sanitize(name) + ".json");
	}

	public static void EnsureDir()
	{
		try
		{
			Directory.CreateDirectory(DirPath);
		}
		catch
		{
		}
	}

	// A profile name is a file name, so characters Windows doesn't allow there become "_".
	public static string Sanitize(string name)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			return "Profile";
		}
		foreach (char c in Path.GetInvalidFileNameChars())
		{
			name = name.Replace(c, '_');
		}
		return name.Trim();
	}

	public static List<string> List()
	{
		EnsureDir();
		try
		{
			return Directory.GetFiles(DirPath, "*.json").Select(Path.GetFileNameWithoutExtension).OrderBy((string n) => n, StringComparer.OrdinalIgnoreCase).ToList();
		}
		catch
		{
			return new List<string>();
		}
	}

	public static bool Exists(string name)
	{
		return File.Exists(FileFor(name));
	}

	// Null when the file is missing or isn't a readable profile.
	public static Profile Load(string name)
	{
		Profile p = Read(FileFor(name));
		if (p != null)
		{
			p.Name = name;
		}
		return p;
	}

	private static Profile Read(string path)
	{
		try
		{
			if (!File.Exists(path))
			{
				return null;
			}
			Profile p = JsonSerializer.Deserialize<Profile>(File.ReadAllText(path));
			if (p != null)
			{
				p.Buttons ??= new List<ButtonBinding>();
				p.Throttle ??= new PedalSettings();
				p.Brake ??= new PedalSettings();
				p.Clutch ??= new PedalSettings();
				if (!p.Throttle.IsValid || !p.Brake.IsValid || !p.Clutch.IsValid)
				{
					return null;
				}
				p.Throttle.Upgrade();
				p.Brake.Upgrade();
				p.Clutch.Upgrade();
				p.WheelRotation = Math.Clamp(p.WheelRotation, Profile.MinWheelRotation, Profile.MaxWheelRotation);
				if (p.Buttons.Any((ButtonBinding b) => b == null || b.Button < 1 || b.Button > byte.MaxValue || b.Vk < 0 || b.Vk > byte.MaxValue))
				{
					return null;
				}
			}
			return p;
		}
		catch
		{
			return null;
		}
	}

	private static bool Write(Profile p, string path)
	{
		try
		{
			File.WriteAllText(path, JsonSerializer.Serialize(p, Json));
			return true;
		}
		catch
		{
			return false;
		}
	}

	public static bool Save(Profile p)
	{
		EnsureDir();
		return Write(p, FileFor(p.Name));
	}

	public static bool Export(Profile p, string path)
	{
		return Write(p, path);
	}

	// The caller picks the imported profile's name.
	public static Profile Import(string path)
	{
		return Read(path);
	}

	public static Profile Clone(Profile p, string name)
	{
		Profile copy = JsonSerializer.Deserialize<Profile>(JsonSerializer.Serialize(p, Json));
		copy.Name = name;
		return copy;
	}

	// The profile already carries its new name. On Windows a change of case only ("gt3" to "GT3") is the same
	// file, so it is moved through a temporary name for the new spelling to stick.
	public static bool Rename(Profile p, string oldName)
	{
		string from = FileFor(oldName);
		string to = FileFor(p.Name);
		try
		{
			if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
			{
				if (File.Exists(from))
				{
					string temp = from + ".renaming";
					File.Move(from, temp, overwrite: true);
					File.Move(temp, to);
				}
				return Save(p);
			}
			if (!Save(p))
			{
				return false;
			}
			Delete(oldName);
			return true;
		}
		catch
		{
			return false;
		}
	}

	public static void Delete(string name)
	{
		try
		{
			File.Delete(FileFor(name));
		}
		catch
		{
		}
	}

	public static void SetActive(string name)
	{
		EnsureDir();
		try
		{
			File.WriteAllText(ActivePath, name);
		}
		catch
		{
		}
	}

	private static string GetActive()
	{
		try
		{
			return File.Exists(ActivePath) ? File.ReadAllText(ActivePath).Trim() : null;
		}
		catch
		{
			return null;
		}
	}

	// The profile to start with: the last one used, else the first, else a new "Default".
	public static Profile Bootstrap()
	{
		List<string> names = List();
		if (names.Count == 0)
		{
			Profile fresh = new Profile();
			Save(fresh);
			SetActive(fresh.Name);
			return fresh;
		}
		string active = GetActive();
		string name = names.FirstOrDefault((string n) => string.Equals(n, active, StringComparison.OrdinalIgnoreCase)) ?? names[0];
		Profile p = Load(name) ?? new Profile
		{
			Name = name
		};
		SetActive(p.Name);
		return p;
	}
}
