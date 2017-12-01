CompositeToggle & StyleSystem
===

## Overview

CompositeToggle & StyleSystem are components that manage snapshots of properties.  
They supports every properties including Image's Sprite, Graphic's Color, Text's text, GameObject's SetActive, etc...  
Set values to properties you need, at same time, without other script.  




## Requirement

* Unity5.3+
* No other SDK




## Usage

1. Download [CompositeToggle.unitypackage](https://github.com/mob-sakai/CompositeToggle/raw/master/CompositeToggle.unitypackage) and install to your project.
2. AddComponent `CompositeToggle` or `Style` to the GameObject.
3. Enjoy!




## How to contorol component's properties in Unity?

In Unity, how to control Images and Text components at the same time?  
How to control the properties, such as sprite, text, color or fontSize?  
It's common case in game development.  

![compositetoggle_switch](https://user-images.githubusercontent.com/12690315/27722542-4a142646-5da3-11e7-825e-a82a7b074ec8.png)

Do you use UnityEvent? Unfortunately, structures such as Color are not supported.  
So, we need a script as following to control the properties.

The script controls 'state (On / Off)' and 'property (sprite etc.)'.  
Therefore, as the state or properties increase, we need to add a script.  
Considering work bottleneck and maintainability, it is not good.  

```cs
[SerializeField] Sprite sptireOn;
[SerializeField] Sprite sptireOff;
[SerializeField] Color colorOn;
[SerializeField] Color colorOff;
[SerializeField] int fontSizeOn;
[SerializeField] int fontSizeOff;

void SwitchOn()
{
    GetComponent<Image>().sprite = sptireOn;
    GetComponent<Image>().color = colorOn;
    GetComponent<Text>().fontSize = fontSizeOn;
}

void SwitchOff()
{
    GetComponent<Image>().sprite = sptireOff;
    GetComponent<Image>().color = colorOff;
    GetComponent<Text>().fontSize = fontSizeOff;
}
...
```



## CompositeToggle

CompositeToggle implemented to separate 'state' and 'property'.  
The state is defined by a script, and the property is defined by a scene or a prefab.  
The script should only turn the toggle on or off. Yes, it is very simple.

![compositetoggle_property](https://user-images.githubusercontent.com/12690315/27763631-d0d5a2a8-5ec1-11e7-8c3b-20840790858e.png)
```cs
[SerializeField] CompositeToggle toggle;

void SetToggleValue(bool isOn)
{
    toggle.booleanValue = isOn;
}
```

CompositeToggle manage snapshots of properties.  
Set values to properties you need, at same time, without other script.  

![compositetoggle_selectproperty](https://user-images.githubusercontent.com/12690315/27722541-4a0ec160-5da3-11e7-99ff-c74df150eb94.png)



## StyleSystem

StyleSystem collectively manages the properties of Component and separates layout and design like CSS.  
A style have some properties you need, and can be referred to by multiple GameObjects.  
When the properties of the style are changed, they are immediately applied to the referencing GameObjects.  

![stylesystem_inspector](https://user-images.githubusercontent.com/12690315/27722545-4a202946-5da3-11e7-975d-50898d4de3b8.png)

In addition, styles can apply at runtime :)

![style_pp](https://user-images.githubusercontent.com/12690315/28653628-8c4b044a-72c9-11e7-9349-3da51fd49c4b.gif)



## Bake Properties To Improve Performance

CompositeToggle internally uses reflection.  
Reflection is slow? -Yes, that's right.  
You can avoid reflection by baking the property to the script.  
Bake the properties from the Style or StyleAsset inspector.  

![image](https://user-images.githubusercontent.com/12690315/28655700-8b2aec10-72d8-11e7-9416-47fd940c3b1f.png)

## Screenshot

* Tabs and views  
![compositetoggle_tabs](https://user-images.githubusercontent.com/12690315/27722543-4a14e9dc-5da3-11e7-993a-bf51adc8da70.gif)

* Indicator  
![compositetoggle_indicator](https://user-images.githubusercontent.com/12690315/27722539-49ebfedc-5da3-11e7-8af5-45deab6d1166.gif)




## Release Notes

### ver.0.1.1:

* Fixed: InspectorGUI issue.
* Fixed: Demo scene.

### ver.0.1.0:

* Feature: Supports every properties of all components.
* Feature: Set values to properties you need, at same time, without other script.
* Feature: Bake properties to script to improve invocation performance.
* Feature: (CompositeToggle) 4 kind of value type.
    * Boolean
    * Index
    * Count
    * Flag
* Feature: (CompositeToggle) Synchronization mode
    * Parent-child relationships
    * Grouping
    * Sync-Other
* Feature: (CompositeToggle) Some features for every toggles.
    * Comment
    * GameObject activation
    * UnityEvent
* Feature: (CompositeToggle) Auto grouping construction based on hierarchy children.
* Feature: (StyleSystem) Style inheritance.
