# 胡闹厨房2 - 厨师外观模组

## 安装

1. 安装 BepInEx 5 (x86)（[GitHub](https://github.com/BepInEx/BepInEx/releases) 或 [百度网盘链接](https://pan.baidu.com/s/1G81rpJNwVsJplJi6fD2jPA?pwd=lobe)），解压后拷贝到游戏根目录下

   <div align="center">
       <img src="bepinex.png" width="30%" height="30%" />
   </div>

   > 开启 `BepInEx` 的控制台可能会导致无法以手柄进入游戏。确保配置文件 `BepInEx/config/BepInEx.cfg` 中 `[Logging.Console]` 组中值为 `Enabled = false`。

2. 将 `OC2DIYChef` 文件夹拷贝到 `BepInEx/plugins` 文件夹中



## 使用

### 隐藏部分厨师

- 将 `OC2DIYChef/official-all.txt` 原地复制一份并重命名为 `prefer.txt`，然后编辑 `prefer.txt`，删去不想使用的厨师的名字。厨师名对应见下图。

  <div align="center">
      <img src="chef_name.png" width="100%" height="100%" />
  </div><br>
  <div align="right" style="font-size:14pt;font-weight:bold">由糯米糍整理</div>
  
- 可以在 `prefer.txt` 中对厨师排序，其中第一个厨师是默认厨师。每次进入游戏或添加本地玩家时会自动选用默认厨师。联机时若你使用额外厨师，对于没有安装此 MOD 的其他玩家显示的是默认厨师。

- 此 MOD 不会修改厨师的解锁状态，包括需要通过关卡或 DLC 解锁的厨师。

- 其他玩家仍会正常显示其选择的厨师，即使你隐藏了该厨师。

### 添加额外厨师

- 将厨师资源文件夹拷贝到 `OC2DIYChef/Resources` 文件夹中，并将厨师资源文件夹名添加到 `prefer.txt` 中即可。
- 在资源文件夹中但未添加到 `prefer.txt` 的额外厨师不会被加载。
- 额外厨师不能作为默认厨师。
- 额外厨师的联机互相显示规则为：当主机安装了此 MOD 且两个玩家都添加了某一个额外厨师时，互相显示该额外厨师；否则选择额外厨师对其他玩家显示的是默认厨师。
- 部分三代厨师[资源](https://pan.baidu.com/s/1EWneQ8k8-P0v49UKs5UpRg?pwd=iu9i)。

> #### 自制额外厨师
>
> - 你可以自制厨师外观。每个自制厨师包括其模型、贴图和材质。
>
> - 参考示例资源格式，你至少需要提供：
>
>   - 头部模型 `Head.obj`，主贴图 `t_Head.png` 以及主材质 `m_Head.txt`；
>   - 手部张开和抓握模型 `Hand_{Grip/Open}_{L/R}.obj`；
>   - `INFO` 文件，其中包含值 `ID=xxx`。额外厨师的 ID 是一个 0 ~ 254 的整数，用作联机消息传递。ID 值 0 ~ 63 预留给三代厨师资源，请选择从 64 开始的值，且最好避开已发布的额外厨师的 ID 值。
>
>   此外还可以提供：
>
>   - 尾巴模型 `Tail.obj`；
>   - 固定在头部的部件模型 `Head{1/2}.obj`；
>   - 眼部模型 `Eyes.obj`（睁眼），`Eyes2_Blinks.obj`（闭眼），`Eyebrows.obj`（眉毛）；
>   - 各模型的单独贴图 / 材质文件（文件名前缀 `t_` / `m_`，无单独贴图 / 材质的模型使用主帖图 / 材质）；
>   - `INFO` 文件中可以添加 `BODY=xxx` 来使用不同的身体模型，例如 `BODY=Chef_Snowman`。

### 替换帽子

在 `prefer.txt` 的每个厨师后面可以（用空格隔开）添加 `HAT=xxx` 用来指定该厨师的帽子样式，可选的有 `None`（不戴）, `Festive`（圣诞）, `Fancy`（厨师帽）, `Baseball`（篝火）。只在关卡内和主菜单界面有效，UI 界面显示的都是 `Fancy` 样式。

### 街机大厅换厨师

在 `prefer.txt` 添加一行 `LOBBYSWITCHCHEF=TRUE` 即可在街机大厅按上下键换厨师。

### 提示消息

- 默认厨师不可用：`prefer.txt` 第一行的厨师必须是已解锁的非额外厨师，否则将设置 Male_Asian 为默认厨师。
- 缺失 INFO 文件：每个额外厨师资源文件夹中必须包含名为 `INFO` 的信息文件。
- 缺失主贴图：每个额外厨师资源文件夹中必须包含名为 `t_Head.png` 的主帖图文件。
- ID 冲突：两个额外厨师的 ID 冲突，或 `INFO` 文件中缺少 ID 值定义。
