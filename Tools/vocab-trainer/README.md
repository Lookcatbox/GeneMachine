# AI 背单词 (Vocab Trainer)

DeepSeek 驱动的智能背单词与学习 Agent 工具。纯前端 + 本地 Python 代理，无需 Unity。

## 功能

- **DeepSeek API**：在「设置」中配置 API Key，经本地代理转发（解决浏览器 CORS）
- **单词导入**：粘贴或上传 `.txt` / `.csv`，格式 `单词 | 释义` 或 `单词,释义`
- **AI 出题**：每个单词至少 6 种题型（释义选择、拼写、填空、近反义、用法、翻译、语境、词根等），**每次学习重新生成**
- **艾宾浩斯复习**：SM-2 变体间隔重复，「复习」页查看待复习词与计划
- **薄弱重练**：一轮学习中正确率 &lt; 70% 或错多种题型的单词，AI 自动加练
- **学习 Agent**：输入任意知识主题与题量，AI 生成专项训练；薄弱知识点临时加题
- **图示题（SVG）**：AI 返回 `diagram` JSON，前端渲染为 SVG；**仅图示补生成**走 `deepseek-v4-pro` + Thinking High，普通出题用设置中的模型
- **总结报告**：每轮结束后 AI 生成 Markdown 报告，错题自动收录到「记录」

## 快速开始

1. 安装 [Python 3](https://www.python.org/)（Windows 一般已自带或可 `py` 启动）
2. 双击 `run.bat`，或在该目录执行：

   ```bash
   python server.py
   ```

3. 浏览器打开 **http://127.0.0.1:8765**
4. 进入「设置」，填入 [DeepSeek API Key](https://platform.deepseek.com/)，点击「测试连接」
5. 「词库」导入单词 → 「学习」或「复习」开始

## 示例

### 导入格式

每行一个单词，使用 ` | `（竖线前后各一个空格）分隔单词与释义：

```
abundant | 丰富的；充裕的
accommodate | 容纳；适应；提供住宿
acquire | 获得；习得
adequate | 足够的；适当的
advocate | 提倡；拥护者
```

也支持 CSV 格式（逗号分隔）：`单词,释义`。

### 示例词库

`samples/ielts-core.txt` 提供了 30 个雅思核心词汇，可直接用于导入测试：

1. 打开应用 → 「词库」标签
2. 点击「从文件导入」选择 `samples/ielts-core.txt`
3. 或直接粘贴文件内容到文本框，点击「导入」

## 目录结构

```
Tools/vocab-trainer/
  index.html      # 主界面
  style.css       # 样式
  server.py       # 静态服务 + API 代理
  run.bat         # Windows 启动脚本
  js/
    app.js        # 主控制器
    api.js        # DeepSeek 调用
    storage.js    # localStorage
    srs.js        # 艾宾浩斯 / SM-2
    prompts.js    # AI 提示词
    answer-judge.js # 开放题 AI 判分
    diagram-ui.js # diagram JSON 转 SVG 图示
    session.js    # 单词学习会话
    agent.js      # 学习 Agent 会话
    question-ui.js
```

## 注意事项

- API Key 仅存于本机浏览器 localStorage，仅发往 DeepSeek 官方 API
- 必须先启动 `server.py`，否则无法调用 AI
- 数据（词库、错题、报告）均在浏览器本地，清除站点数据会丢失

## 与 Claude Code 协作

本项目由 Cursor 主 agent 搭建架构，可通过 `.cursor/delegations/` 将 UI 润色、示例词库等子任务委托给 Claude Code CLI 执行。
