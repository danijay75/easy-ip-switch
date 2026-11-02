using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace EasyIpSwitch
{
    // Synthetic adapters and text boxes only: no network settings are changed.
    internal static class StaticDefaultsTests
    {
        private static int _assertions;

        public static int Run()
        {
            StaticDefaultsAndGatewayValidation();
            SuggestedGatewayRules();
            GatewayIsSuggestedFromStaticAddress();
            LiveValidationTracksAllFields();
            DraftsSurviveModeSwitchAndRefresh();
            ExistingStaticConfigurationIsPreserved();
            ResponsiveLayoutReflowsWithWidth();
            InvalidBorderFadesAndStops();
            return _assertions;
        }

        private static AdapterInfo DhcpInfo()
        {
            AdapterInfo info = new AdapterInfo();
            info.Name = "Synthetic adapter";
            info.Description = "Synthetic Ethernet adapter";
            info.Index = 9999;
            info.Enabled = true;
            info.Connected = true;
            info.Dhcp = true;
            info.Ip = "10.0.0.50";
            info.Mask = "255.255.255.0";
            info.Gateway = "10.0.0.1";
            return info;
        }

        private static void StaticDefaultsAndGatewayValidation()
        {
            using (AdapterCard card = new AdapterCard(DhcpInfo()))
            {
                InputBox ip = Field<InputBox>(card, "_ip");
                InputBox mask = Field<InputBox>(card, "_mask");
                InputBox gateway = Field<InputBox>(card, "_gateway");
                Field<SegmentedControl>(card, "_segment").SelectedIndex = 1;

                Assert(ip.Value == "192.168.0.1", "Static mode must use the requested default IP.");
                Assert(mask.Value == "255.255.255.0", "The current subnet mask must be retained.");
                Assert(gateway.Value == "192.168.0.2", "The gateway must be suggested from the default address.");
                Assert(!ip.Invalid && !mask.Invalid && !gateway.Invalid, "The suggested gateway must be valid immediately.");
                Assert(Field<FlatButton>(card, "_btnApply").Enabled, "A valid pending change must remain applicable.");

                TypeText(ip, "192.168.0.2");
                Assert(gateway.Value == "192.168.0.1", "The suggested gateway must follow the address.");

                TypeText(gateway, "192.168.0.2");
                Assert(gateway.Invalid, "A gateway identical to the IP must be highlighted immediately.");
                Assert(Field<Label>(card, "_lblError").Text.IndexOf("différente", StringComparison.Ordinal) >= 0, "The conflict must be explained inline.");
                ValidationResult invalid = card.ValidateInputs();
                Assert(!invalid.IsValid && object.ReferenceEquals(invalid.Field, gateway), "Identical IP and gateway must not be applied.");

                TypeText(ip, "192.168.0.3");
                Assert(!gateway.Invalid && !ip.Invalid, "Correcting the IP must clear the gateway conflict immediately.");
                Assert(card.ValidateInputs().IsValid, "A valid IP and gateway must be accepted.");
            }

            AdapterInfo emptyMask = DhcpInfo();
            emptyMask.Mask = "";
            using (AdapterCard card = new AdapterCard(emptyMask))
            {
                Field<SegmentedControl>(card, "_segment").SelectedIndex = 1;
                Assert(Field<InputBox>(card, "_mask").Value == "255.255.255.0", "An empty mask must receive a usable default.");
            }

            AdapterInfo customMask = DhcpInfo();
            customMask.Mask = "255.255.0.0";
            using (AdapterCard card = new AdapterCard(customMask))
            {
                Field<SegmentedControl>(card, "_segment").SelectedIndex = 1;
                Assert(Field<InputBox>(card, "_mask").Value == "255.255.0.0", "An existing subnet mask must not be overwritten.");
            }
        }

        private static void SuggestedGatewayRules()
        {
            Assert(IpUtil.SuggestGateway("192.168.2.101", "255.255.255.0") == "192.168.2.1",
                "192.168.2.101/24 must suggest 192.168.2.1.");
            Assert(IpUtil.SuggestGateway("192.168.2.1", "255.255.255.0") == "192.168.2.2",
                "The first host must suggest the next usable host.");
            Assert(IpUtil.SuggestGateway("10.0.0.50", "255.255.0.0") == "10.0.0.1",
                "A /16 address must suggest the first host of its subnet.");
            Assert(IpUtil.SuggestGateway("169.254.1.1", "255.255.0.0") == null,
                "An APIPA address must not produce a gateway.");
            Assert(IpUtil.SuggestGateway("0.0.0.0", "255.255.255.0") == null,
                "A forbidden address must not produce a gateway.");
            Assert(IpUtil.SuggestGateway("192.168.2.101", "255.0.255.0") == null,
                "A non-contiguous mask must not produce a gateway.");
            Assert(IpUtil.SuggestGateway("192.168.2.101", "255.255.255.254") == null,
                "A /31 mask must not produce a gateway.");
        }

        private static void GatewayIsSuggestedFromStaticAddress()
        {
            using (AdapterCard card = new AdapterCard(DhcpInfo()))
            {
                Field<SegmentedControl>(card, "_segment").SelectedIndex = 1;
                InputBox ip = Field<InputBox>(card, "_ip");
                InputBox mask = Field<InputBox>(card, "_mask");
                InputBox gateway = Field<InputBox>(card, "_gateway");

                TypeText(ip, "192.168.2.101");
                Assert(gateway.Value == "192.168.2.1", "192.168.2.101/24 must suggest 192.168.2.1.");

                TypeText(mask, "255.255.0.0");
                Assert(gateway.Value == "192.168.0.1", "The suggestion must follow the mask.");

                TypeText(ip, "10.5.0.20");
                Assert(gateway.Value == "10.5.0.1", "The suggestion must follow the address.");

                TypeText(gateway, "10.5.0.254");
                TypeText(ip, "10.6.0.20");
                Assert(gateway.Value == "10.5.0.254", "A manually entered gateway must never be overwritten.");

                TypeText(gateway, "");
                TypeText(ip, "10.7.0.20");
                Assert(gateway.Value == "", "A gateway cleared by the user must stay empty.");
            }
        }

        private static void LiveValidationTracksAllFields()
        {
            using (AdapterCard card = new AdapterCard(DhcpInfo()))
            {
                Field<SegmentedControl>(card, "_segment").SelectedIndex = 1;
                InputBox ip = Field<InputBox>(card, "_ip");
                InputBox mask = Field<InputBox>(card, "_mask");
                InputBox gateway = Field<InputBox>(card, "_gateway");

                TypeText(ip, "192.168.0.999");
                TypeText(mask, "255.0.255.0");
                TypeText(gateway, "300.0.0.1");
                Assert(ip.Invalid && mask.Invalid && gateway.Invalid, "Every invalid field must remain highlighted while typing.");
                Assert(!card.ValidateInputs().IsValid, "Invalid values must block application.");
                card.UpdateInfo(DhcpInfo(), false);
                Assert(ip.Invalid && mask.Invalid && gateway.Invalid, "Automatic refresh must preserve invalid indicators.");
                Assert(ip.Value == "192.168.0.999" && mask.Value == "255.0.255.0" && gateway.Value == "300.0.0.1", "Automatic refresh must preserve invalid drafts.");

                TypeText(ip, "192.168.0.2");
                Assert(!ip.Invalid && mask.Invalid && gateway.Invalid, "Correcting one field must not clear other errors.");
                TypeText(mask, "255.255.255.0");
                Assert(!mask.Invalid && gateway.Invalid, "Correcting the mask must retain the gateway error.");
                TypeText(gateway, "");
                Assert(!gateway.Invalid && card.ValidateInputs().IsValid, "An empty optional gateway must be allowed.");

                TypeText(ip, "");
                Assert(ip.Invalid && !card.ValidateInputs().IsValid, "An empty required IP must remain invalid.");
                TypeText(ip, "192.168.0.2");
                Assert(!ip.Invalid && card.ValidateInputs().IsValid, "The IP error must disappear when corrected.");
            }
        }

        private static void DraftsSurviveModeSwitchAndRefresh()
        {
            using (AdapterCard card = new AdapterCard(DhcpInfo()))
            {
                SegmentedControl mode = Field<SegmentedControl>(card, "_segment");
                InputBox ip = Field<InputBox>(card, "_ip");
                InputBox gateway = Field<InputBox>(card, "_gateway");
                mode.SelectedIndex = 1;
                TypeText(ip, "192.168.0.42");
                TypeText(gateway, "192.168.0.1");
                mode.SelectedIndex = 0;
                Assert(ip.Value == "10.0.0.50" && gateway.Value == "10.0.0.1", "DHCP mode must show the current network values.");

                card.UpdateInfo(DhcpInfo(), false);
                mode.SelectedIndex = 1;
                Assert(ip.Value == "192.168.0.42" && gateway.Value == "192.168.0.1", "Switching back must restore the static draft.");
                card.UpdateInfo(DhcpInfo(), false);
                Assert(ip.Value == "192.168.0.42" && gateway.Value == "192.168.0.1", "An automatic refresh must preserve pending static edits.");

                card.ResetToCurrent();
                Assert(mode.SelectedIndex == 0 && ip.Value == "10.0.0.50", "Cancel must restore the real DHCP state.");
                mode.SelectedIndex = 1;
                Assert(ip.Value == "192.168.0.1" && gateway.Value == "192.168.0.2", "After cancel, static defaults and their suggested gateway must be restored.");
            }
        }

        private static void ExistingStaticConfigurationIsPreserved()
        {
            AdapterInfo info = DhcpInfo();
            info.Dhcp = false;
            using (AdapterCard card = new AdapterCard(info))
            {
                SegmentedControl mode = Field<SegmentedControl>(card, "_segment");
                InputBox ip = Field<InputBox>(card, "_ip");
                InputBox gateway = Field<InputBox>(card, "_gateway");
                Assert(mode.SelectedIndex == 1 && ip.Value == "10.0.0.50" && gateway.Value == "10.0.0.1", "An existing static configuration must be displayed unchanged.");
                mode.SelectedIndex = 0;
                mode.SelectedIndex = 1;
                Assert(ip.Value == "10.0.0.50" && gateway.Value == "10.0.0.1", "Switching modes must not overwrite an existing static configuration.");
                TypeText(ip, "10.0.0.51");
                Assert(gateway.Value == "10.0.0.1", "A gateway read from the configuration must stay in its subnet.");
                Assert(Field<FlatButton>(card, "_btnApply").Enabled, "Editing a static IP must enable its apply button.");
                TypeText(ip, "10.0.0.50");
                Assert(!Field<FlatButton>(card, "_btnApply").Enabled, "Restoring the current static IP must clear the pending change.");

                TypeText(ip, "192.168.2.101");
                Assert(gateway.Value == "192.168.2.1", "An existing static gateway must follow a subnet change.");
            }
        }

        private static void ResponsiveLayoutReflowsWithWidth()
        {
            using (AdapterCard card = new AdapterCard(DhcpInfo()))
            {
                Label name = Field<Label>(card, "_lblName");
                InputBox ip = Field<InputBox>(card, "_ip");
                InputBox gateway = Field<InputBox>(card, "_gateway");

                card.Width = Theme.S(1050);
                Assert(card.Height == Theme.S(196), "A wide card must keep its compact height.");
                Assert(gateway.Top == ip.Top, "A wide card must show the three fields on one row.");

                card.Width = Theme.S(830);
                Assert(card.Height > Theme.S(196), "A narrow card must grow to stack its content.");
                Assert(gateway.Top > ip.Top, "A narrow card must move the gateway below the IP and mask.");
                Assert(name.Height >= Theme.S(40) && !name.AutoEllipsis,
                    "Long service names must be allowed to wrap instead of being truncated.");

                card.Width = Theme.S(560);
                Assert(gateway.Top > ip.Top, "A very narrow card must stack all three fields.");
                Assert(card.Height > Theme.S(196), "The stacked card must reserve the required height.");
            }
        }

        private static void InvalidBorderFadesAndStops()
        {
            Color first = InputBox.InvalidBorderColor(0);
            Color later = InputBox.InvalidBorderColor(550);
            Assert(first.ToArgb() != later.ToArgb(), "The invalid border must fade over time.");
            Assert(first.R > first.G && later.R > later.G, "Both animation colors must remain light red.");

            using (InputBox box = new InputBox())
            {
                box.CreateControl();
                box.Invalid = true;
                Timer timer = Field<Timer>(box, "_invalidTimer");
                Assert(timer != null && timer.Enabled, "A visible invalid field must animate.");
                box.Visible = false;
                Assert(!timer.Enabled, "A hidden field must not keep its timer running.");
                box.Visible = true;
                Assert(timer.Enabled, "The animation must resume when the invalid field is visible.");
                box.Invalid = false;
                Assert(!timer.Enabled, "The animation must stop when the field is corrected.");
            }
        }

        private static void TypeText(InputBox box, string value)
        {
            Field<TextBox>(box, "_text").Text = value;
        }

        private static T Field<T>(object instance, string name)
        {
            FieldInfo field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                throw new InvalidOperationException("Missing field: " + name);
            }
            return (T)field.GetValue(instance);
        }

        private static void Assert(bool condition, string message)
        {
            _assertions++;
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
