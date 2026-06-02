# ChemistryReactionEditor

外部化学反应编辑器。用于编辑游戏运行时读取的：

`Assets/StreamingAssets/chemistry-reactions.json`

## 运行

在项目根目录或本目录运行：

```powershell
dotnet run --project Tools/ChemistryReactionEditor/ChemistryReactionEditor.csproj
```

也可以双击 `run-editor.bat`。

## 使用

1. 打开 JSON 配置文件。
2. 在“物质”表中编辑 `id`、显示名、形态、颜色、热力图上限、陆地/水域基准量。
3. 在“反应”列表中新增、删除、拖拽排序。列表越靠上，保存后的优先级越高。
4. 在右侧编辑反应物、生成物、条件表达式和动力方程表达式。
5. 点击“验证”，无错误后点击“保存”。

## 表达式变量

- `tempC`
- `light`
- `height`
- `topography`
- `limiting`
- `amount["substanceId"]`
- `reactantCoeff["substanceId"]`
- `productCoeff["substanceId"]`

## 表达式函数

- `min(a,b)`
- `max(a,b)`
- `clamp(x,min,max)`
- `pow(x,p)`
- `abs(x)`
