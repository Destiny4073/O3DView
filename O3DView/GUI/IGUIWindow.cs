namespace O3DView.GUI;

public interface IGUIWindow
{
    public static IGUIWindow Construct(GUIController guiController, Program program)
    {
        throw new NotImplementedException();
    }

    public abstract bool DrawWindow();
}
