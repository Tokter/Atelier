using System;
using System.Collections.ObjectModel;
using Atelier.Controls;
using Atelier.Core.Keybinding;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Atelier.Gallery.ViewModels;

/// <summary>A person in the contact list demo.</summary>
public sealed record Contact(string Name, string Email, string Role, bool IsOnline)
{
    public string Initials => string.Concat(Array.ConvertAll(Name.Split(' ', StringSplitOptions.RemoveEmptyEntries), p => p[..1]));

    // Text search (typing in the list) matches the item's text.
    public override string ToString() => Name;
}

public partial class ListsViewModel : PageViewModel
{
    /// <summary>The keybinding group of this page's commands.</summary>
    public const string Group = "Lists";

    private static readonly Contact[] SampleContacts =
    [
        new("Ada Lovelace", "ada@example.com", "Mathematician", true),
        new("Alan Turing", "alan@example.com", "Computer scientist", false),
        new("Barbara Liskov", "barbara@example.com", "Language designer", true),
        new("Donald Knuth", "don@example.com", "Author", false),
        new("Grace Hopper", "grace@example.com", "Rear admiral", true),
        new("Katherine Johnson", "katherine@example.com", "Mathematician", false),
        new("Linus Torvalds", "linus@example.com", "Kernel maintainer", true),
        new("Margaret Hamilton", "margaret@example.com", "Software engineer", false),
    ];

    private int _added;

    [ObservableProperty]
    private bool _controlsEnabled = true;

    public ObservableCollection<Contact> Contacts { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveSelectedCommand), nameof(MoveUpCommand), nameof(MoveDownCommand))]
    private Contact? _selectedContact;

    [ObservableProperty]
    private int _selectedIndex = -1;

    [ObservableProperty]
    private string _lastSelectionChanged = "Select a contact";

    [ObservableProperty]
    private string _selectedFruit = "(none)";

    [ObservableProperty]
    private bool _isTextSearchEnabled = true;

    public ObservableCollection<string> Tags { get; } = ["Design", "C#", "Layout", "Material 3"];

    [ObservableProperty]
    private string _newTag = string.Empty;

    [ObservableProperty]
    private string _lastItemClicked = "Click an item";

    public ListsViewModel()
    {
        PageIcon = MaterialIconKind.ViewList;
        PageTitle = "Lists";
        CommandGroup = Group;
        Keywords = "listbox itemscontrol listboxitem list items selection template";
        ResetContacts();
    }

    [RelayCommand]
    [property: Command("AddContact", Group, Label = "Add", Icon = MaterialIcons.Add, Description = "Add a contact to the bound collection")]
    private void AddContact()
    {
        _added++;
        var contact = new Contact($"New Contact {_added}", $"new{_added}@example.com", "Guest", _added % 2 == 0);
        Contacts.Add(contact);
        SelectedContact = contact;
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    [property: Command("RemoveSelected", Group, Label = "Remove", Icon = MaterialIcons.Delete, Description = "Remove the selected contact")]
    private void RemoveSelected()
    {
        int index = SelectedContact is { } contact ? Contacts.IndexOf(contact) : -1;
        if (index < 0)
        {
            return;
        }

        Contacts.RemoveAt(index);
        SelectedContact = Contacts.Count > 0 ? Contacts[Math.Min(index, Contacts.Count - 1)] : null;
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    [property: Command("MoveUp", Group, Label = "Up", Icon = MaterialIcons.ArrowUpward, Description = "Move the selected contact up")]
    private void MoveUp() => Move(-1);

    [RelayCommand(CanExecute = nameof(HasSelection))]
    [property: Command("MoveDown", Group, Label = "Down", Icon = MaterialIcons.ArrowDownward, Description = "Move the selected contact down")]
    private void MoveDown() => Move(1);

    private void Move(int delta)
    {
        if (SelectedContact is not { } contact)
        {
            return;
        }

        int index = Contacts.IndexOf(contact);
        int target = index + delta;
        if (index >= 0 && target >= 0 && target < Contacts.Count)
        {
            Contacts.Move(index, target);
        }
    }

    private bool HasSelection() => SelectedContact != null;

    [RelayCommand]
    [property: Command("ClearContacts", Group, Label = "Clear", Description = "Remove all contacts")]
    private void ClearContacts() => Contacts.Clear();

    [RelayCommand]
    [property: Command("ResetContacts", Group, Label = "Restore", Description = "Bring back the sample contacts")]
    private void ResetContacts()
    {
        Contacts.Clear();
        foreach (var contact in SampleContacts)
        {
            Contacts.Add(contact);
        }
        // SelectedItem and SelectedIndex are both bound two-way, so they must agree: an index of -1 would clear the item.
        SelectedContact = Contacts[0];
        SelectedIndex = 0;
    }

    [RelayCommand]
    [property: Command("AddTag", Group, Label = "Add tag", Icon = MaterialIcons.Add, Description = "Add the typed tag")]
    private void AddTag()
    {
        string tag = NewTag.Trim();
        if (tag.Length > 0 && !Tags.Contains(tag))
        {
            Tags.Add(tag);
        }
        NewTag = string.Empty;
    }

    [RelayCommand]
    [property: Command("RemoveLastTag", Group, Label = "Remove last", Description = "Remove the last tag")]
    private void RemoveLastTag()
    {
        if (Tags.Count > 0)
        {
            Tags.RemoveAt(Tags.Count - 1);
        }
    }

    [RelayCommand]
    [property: Command("Reset", Group, Icon = MaterialIcons.RestartAlt, Description = "Put the demos of this page back as they were")]
    private void Reset()
    {
        ControlsEnabled = true;
        IsTextSearchEnabled = true;
        ResetContacts();
        Tags.Clear();
        foreach (var tag in new[] { "Design", "C#", "Layout", "Material 3" })
        {
            Tags.Add(tag);
        }
    }
}
