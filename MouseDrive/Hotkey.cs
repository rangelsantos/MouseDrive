using System;

namespace MouseDrive;

// A rebindable command, on a key or a mouse button. The binding is stored on the Profile, so each car or
// game keeps its own; 0 means unassigned. Defaults come from a fresh Profile's initializers.
public sealed class Hotkey
{
	private static readonly Profile Defaults = new Profile();

	public static readonly Hotkey[] All = new Hotkey[2]
	{
		new Hotkey("DRIVING", "Drive / standby", (Profile p) => p.ToggleVk, (Profile p, int vk) => p.ToggleVk = vk),
		new Hotkey("DRIVING", "Center steering", (Profile p) => p.CenterVk, (Profile p, int vk) => p.CenterVk = vk)
	};

	private readonly Func<Profile, int> get;

	private readonly Action<Profile, int> set;

	public string Group { get; }

	public string Label { get; }

	public int DefaultVk => get(Defaults);

	private Hotkey(string group, string label, Func<Profile, int> get, Action<Profile, int> set)
	{
		Group = group;
		Label = label;
		this.get = get;
		this.set = set;
	}

	public int Get(Profile p)
	{
		return get(p);
	}

	public void Set(Profile p, int vk)
	{
		set(p, vk);
	}
}
