using System;
using System.Collections.Generic;
using Atelier.Core.Inspection;
using Xunit;

namespace Atelier.Tests;

#region Test Models

[Inspectable]
public partial class SettingsModel
{
    [InspectableProperty("Server Host", "Network")]
    public string Host { get; set; } = "localhost";

    [InspectableProperty("Port Number", "Network")]
    public int Port { get; set; } = 8080;

    [InspectableProperty("Use SSL", "Security")]
    public bool EnableSsl { get; set; } = true;
}

[Inspectable]
public partial class ReadOnlyModel
{
    public string Id { get; } = "ABC-123";
    public int ComputedValue => 42;

    [InspectableProperty(IsReadOnly = true)]
    public string ConfigName { get; set; } = "Default";
}

[Inspectable]
public partial class ModelWithIgnoredProperty
{
    public string VisibleProp { get; set; } = "Show";

    [InspectableIgnore]
    public string SecretProp { get; set; } = "Hide";
}

[Inspectable]
public partial class ModelWithStandardAttributes
{
    [System.ComponentModel.DisplayName("User Alias")]
    [System.ComponentModel.Category("Account")]
    public string Username { get; set; } = "alice";

    [InspectableProperty(Order = 2)]
    public int Second { get; set; } = 2;

    [InspectableProperty(Order = 1)]
    public int First { get; set; } = 1;
}

public partial class OuterContainer
{
    [Inspectable]
    public partial class NestedInspectable
    {
        public string Title { get; set; } = "Nested";
    }
}

[Inspectable]
public partial record DeviceRecord(string DeviceName, int BatteryLevel);

#endregion

public class InspectableTests
{
    [Fact]
    public void SettingsModel_GeneratedDescriptorTable_MatchesUserSpecification()
    {
        var model = new SettingsModel();

        // Must implement IInspectableObject
        Assert.IsAssignableFrom<IInspectableObject>(model);

        var properties = model.GetProperties();
        Assert.NotNull(properties);
        Assert.Equal(3, properties.Count);

        // 1. Host property
        var hostProp = properties[0];
        Assert.Equal(nameof(SettingsModel.Host), hostProp.Name);
        Assert.Equal("Server Host", hostProp.DisplayName);
        Assert.Equal("Network", hostProp.Category);
        Assert.Equal(typeof(string), hostProp.PropertyType);
        Assert.False(hostProp.IsReadOnly);
        Assert.Equal("localhost", hostProp.GetValue(model));

        // 2. Port property
        var portProp = properties[1];
        Assert.Equal(nameof(SettingsModel.Port), portProp.Name);
        Assert.Equal("Port Number", portProp.DisplayName);
        Assert.Equal("Network", portProp.Category);
        Assert.Equal(typeof(int), portProp.PropertyType);
        Assert.False(portProp.IsReadOnly);
        Assert.Equal(8080, portProp.GetValue(model));

        // 3. EnableSsl property
        var sslProp = properties[2];
        Assert.Equal(nameof(SettingsModel.EnableSsl), sslProp.Name);
        Assert.Equal("Use SSL", sslProp.DisplayName);
        Assert.Equal("Security", sslProp.Category);
        Assert.Equal(typeof(bool), sslProp.PropertyType);
        Assert.False(sslProp.IsReadOnly);
        Assert.Equal(true, sslProp.GetValue(model));
    }

    [Fact]
    public void SetValue_MutatesPropertiesWithoutReflection()
    {
        var model = new SettingsModel();
        var properties = model.GetProperties();

        var hostProp = properties[0];
        hostProp.SetValue(model, "remote.server.internal");
        Assert.Equal("remote.server.internal", model.Host);

        var portProp = properties[1];
        portProp.SetValue(model, 9443);
        Assert.Equal(9443, model.Port);

        var sslProp = properties[2];
        sslProp.SetValue(model, false);
        Assert.False(model.EnableSsl);
    }

    [Fact]
    public void StronglyTypedAccessors_ProvideZeroBoxing()
    {
        var model = new SettingsModel();
        var properties = model.GetProperties();

        var typedPort = properties[1] as PropertyDescriptor<SettingsModel, int>;
        Assert.NotNull(typedPort);

        // Strongly typed getter
        int portVal = typedPort.GetTypedValue(model);
        Assert.Equal(8080, portVal);

        // Strongly typed setter
        typedPort.SetTypedValue(model, 3000);
        Assert.Equal(3000, model.Port);
    }

    [Fact]
    public void ReadOnlyProperties_DetectedAndPreventMutation()
    {
        var model = new ReadOnlyModel();
        var properties = model.GetProperties();

        Assert.Equal(3, properties.Count);

        var idProp = properties[0];
        Assert.Equal("Id", idProp.Name);
        Assert.True(idProp.IsReadOnly);
        Assert.Equal("ABC-123", idProp.GetValue(model));
        Assert.Throws<InvalidOperationException>(() => idProp.SetValue(model, "XYZ-999"));

        var computedProp = properties[1];
        Assert.Equal("ComputedValue", computedProp.Name);
        Assert.True(computedProp.IsReadOnly);
        Assert.Equal(42, computedProp.GetValue(model));
        Assert.Throws<InvalidOperationException>(() => computedProp.SetValue(model, 100));

        var explicitRoProp = properties[2];
        Assert.Equal("ConfigName", explicitRoProp.Name);
        Assert.True(explicitRoProp.IsReadOnly);
        Assert.Throws<InvalidOperationException>(() => explicitRoProp.SetValue(model, "Custom"));
    }

    [Fact]
    public void IgnoredProperties_AreExcludedFromDescriptorTable()
    {
        var model = new ModelWithIgnoredProperty();
        var properties = model.GetProperties();

        Assert.Single(properties);
        Assert.Equal("VisibleProp", properties[0].Name);
        Assert.Null(ObjectInspector.GetProperty(model, "SecretProp"));
    }

    [Fact]
    public void StandardAttributes_AndOrder_AreHonored()
    {
        var model = new ModelWithStandardAttributes();
        var properties = model.GetProperties();

        Assert.Equal(3, properties.Count);

        // Order: First (Order=1), Second (Order=2), Username (Order=0 default)
        // Wait, Order=0 is first if sorted ascending:
        // Order 0: Username
        // Order 1: First
        // Order 2: Second
        Assert.Equal("Username", properties[0].Name);
        Assert.Equal("User Alias", properties[0].DisplayName);
        Assert.Equal("Account", properties[0].Category);

        Assert.Equal("First", properties[1].Name);
        Assert.Equal("Second", properties[2].Name);
    }

    [Fact]
    public void NestedClass_GeneratesValidInspection()
    {
        var nested = new OuterContainer.NestedInspectable();
        var properties = nested.GetProperties();

        Assert.Single(properties);
        Assert.Equal("Title", properties[0].Name);
        Assert.Equal("Nested", properties[0].GetValue(nested));

        properties[0].SetValue(nested, "Updated");
        Assert.Equal("Updated", nested.Title);
    }

    [Fact]
    public void RecordType_GeneratesValidInspection()
    {
        var record = new DeviceRecord("iPad Pro", 85);
        var properties = record.GetProperties();

        Assert.Equal(2, properties.Count);
        Assert.Equal("DeviceName", properties[0].Name);
        Assert.Equal("iPad Pro", properties[0].GetValue(record));

        Assert.Equal("BatteryLevel", properties[1].Name);
        Assert.Equal(85, properties[1].GetValue(record));
    }

    [Fact]
    public void ObjectInspector_InspectsAnyObjectSafely()
    {
        var model = new SettingsModel();

        // 1. Inspect inspectable object
        var props = ObjectInspector.GetProperties(model);
        Assert.Equal(3, props.Count);

        // 2. Inspect non-inspectable object returns empty
        var emptyProps = ObjectInspector.GetProperties("plain string");
        Assert.Empty(emptyProps);

        // 3. Inspect null returns empty
        Assert.Empty(ObjectInspector.GetProperties(null));

        // 4. TryGetValue
        Assert.True(ObjectInspector.TryGetValue(model, "Host", out var hostVal));
        Assert.Equal("localhost", hostVal);
        Assert.False(ObjectInspector.TryGetValue(model, "NonExistent", out _));

        // 5. TrySetValue
        Assert.True(ObjectInspector.TrySetValue(model, "Port", 9000));
        Assert.Equal(9000, model.Port);
        Assert.False(ObjectInspector.TrySetValue(model, "NonExistent", 123));
    }
}
