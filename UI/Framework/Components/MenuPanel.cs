using UnityEngine.UIElements;

namespace Saga.UI
{
    [UxmlElement]
    public partial class MenuPanel : VisualElement
    {
        public const string UssClassName = "menu";
        public const string BodyUssClassName = UssClassName + "__panel";
        public const string TitleUssClassName = UssClassName + "__title";

        readonly VisualElement body;
        readonly Label titleLabel;

        // UXML children land in the panel body, under the title, instead of beside the scrim
        public override VisualElement contentContainer => body;

        [UxmlAttribute]
        public string Title
        {
            get => titleLabel.text;
            set => titleLabel.text = value;
        }

        public MenuPanel()
        {
            AddToClassList(UssClassName);
            body = new VisualElement();
            body.AddToClassList(BodyUssClassName);
            hierarchy.Add(body);
            titleLabel = new Label();
            titleLabel.AddToClassList(TitleUssClassName);
            body.Add(titleLabel);
        }
    }
}
