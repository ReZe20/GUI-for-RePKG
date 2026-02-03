![play_store_feature_graphic.png](https://github.com/ReZe20/GUI-for-RePKG/blob/master/docs/1.png)

# <div align="center">基于[**<span style="color: blue;">**RePKG**</span>**](https://github.com/notscuffed/repkg)开发的Wallpaper Engine壁纸提取器</div>

### <div align="center">读取Wallpaper Engine内已安装壁纸，后台利用RePKG命令行工具转换壁纸</div>

![image.png](https://github.com/ReZe20/GUI-for-RePKG/blob/master/docs/2.png)

# 快速上手

## 壁纸转换

### 已有壁纸

#### 目录读取

程序自动读取Wallpaper Engine位置并扫描.pkg文件，若显示“所选壁纸路径无壁纸”，请在设置中的启动设置里输入壁纸路径

常见壁纸路径如下，一般含有"\content\43196"

![image.png](https://github.com/ReZe20/GUI-for-RePKG/blob/master/docs/3.png)

#### 筛选结果

按下左上角的 >> 按钮可展开结果筛选栏目，所有结果默认勾选状态
![筛选结果.gif](https://github.com/ReZe20/GUI-for-RePKG/blob/master/docs/4.gif)
通过右上角的搜索框搜索，按下回车键或者单击右边的搜索键进行搜索，当搜索栏为空时将恢复所有结果
![搜索结果.gif](https://github.com/ReZe20/GUI-for-RePKG/blob/master/docs/5.gif)

#### 多选结果

选中图片左上角的复选框即可开启多选模式，多选模式下单击图片即可选中

![按下多选框多选.gif](https://github.com/ReZe20/GUI-for-RePKG/blob/master/docs/6.gif)

按下多选按钮也可开始多选，多选模式下再次按下此按钮，按下全选与反选按钮自动打开多选模式

![多选按钮.gif](https://github.com/ReZe20/GUI-for-RePKG/blob/master/docs/7.gif)

#### 转换壁纸

转换前需在壁纸预览上方设定导出目录，本程序默认设定桌面为输出目录，请勿设置带有空格的目录

![导出目录.gif](https://github.com/ReZe20/GUI-for-RePKG/blob/master/docs/8.gif)

单击开始转换按钮，程序将转换所有复选框勾选的壁纸，具体转换详情依据设置

#### 导入壁纸编辑器

勾选所需壁纸后单击导入壁纸编辑器按钮，支持多壁纸导入

## 导入壁纸

可将文件壁纸拖拽至文本框内，支持多文件导入

![多文件拖拽.gif](https://github.com/ReZe20/GUI-for-RePKG/blob/master/docs/9.gif)

也可拖入文件夹，程序将扫描文件夹内后缀名为.pkg或.mpkg的文件。本程序一次只能识别一个文件夹

![文件夹拖拽.gif](https://github.com/ReZe20/GUI-for-RePKG/blob/master/docs/10.gif)

#### ！！注意 ！！

开始转换前请先确认“导入壁纸”与“导入文件夹”内的输出路径有且只有一个文本框不为空，本程序在输出时会读取有内容的文本框作为输出路径![导出路径设置.gif](https://github.com/ReZe20/GUI-for-RePKG/blob/master/docs/11.gif)

---

## 设置

#### 以壁纸名作为目录名

![image.png](https://github.com/ReZe20/GUI-for-RePKG/blob/master/docs/12.png)

此功能仅在“已有壁纸”页面中生效

勾选后，程序将先用一个壁纸的ID作为导出文件夹名进行导出，该壁纸导出完成后，程序按照壁纸名更改这个文件夹

![壁纸名作文件夹名.gif](https://github.com/ReZe20/GUI-for-RePKG/blob/master/docs/13.gif)

遇到Windows系统目录非法字符(<>: " / \\ | ? *)将用 _ 字符替代

#### 只转换TEX文件

![image.png](https://github.com/ReZe20/GUI-for-RePKG/blob/master/docs/14.png)

勾选后导出文件夹内只出现materials文件夹，主要图片保存在此处

![image.png](https://github.com/ReZe20/GUI-for-RePKG/blob/master/docs/15.png)

#### 不将TEX文件转换为图像

![image.png](https://github.com/ReZe20/GUI-for-RePKG/blob/master/docs/16.png)

勾选后遇到TEX文件直接保存，不进行转换

倘若同时勾选“只转换TEX文件”，程序只保存TEX文件至输出目录

#### 将所有提取的文件放入一个文件夹

![image.png](https://github.com/ReZe20/GUI-for-RePKG/blob/master/docs/17.png)

勾选后所有文件放在导出文件夹中，无额外子文件夹

![不保存子目录.png](https://github.com/ReZe20/GUI-for-RePKG/blob/master/docs/18.png)

#### 多实例并行支持

![image.png](https://github.com/ReZe20/GUI-for-RePKG/blob/master/docs/19.png)

程序识别到多个逻辑处理器时将自动开启多实例并行支持，默认设置为逻辑处理器数量

此设置旨在提高多壁纸转换速度，具体速度由处理器速度和硬盘速度决定
建议使用高速硬盘进行壁纸导入与输出

在使用机械硬盘时请降低实例数或者关闭多实例支持，避免整体转换速度降低

![实例测速.png](https://github.com/ReZe20/GUI-for-RePKG/blob/master/docs/20.png)
由8核心16线程CPU测试所得数据

程序在运行时按照所选实例数在后台创建RePKG应用任务，转换过程中结束本程序将会同时结束所有由本程序创建的RePKG实例

![结束任务.gif](https://github.com/ReZe20/GUI-for-RePKG/blob/master/docs/21.gif)

