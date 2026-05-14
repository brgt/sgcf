import{d as m,c as u,r as g,b,t as T,n as v,o as f}from"./vue.esm-bundler-Sd8zNG93.js";import{_ as y}from"./_plugin-vue_export-helper-DlAUqK2U.js";const x={class:"nw-tooltip"},c=m({__name:"Tooltip",props:{content:{},placement:{default:"top"}},setup(t){return(d,h)=>(f(),u("div",x,[g(d.$slots,"default",{},void 0,!0),b("span",{class:v(["nw-tooltip__content",`nw-tooltip__content--${t.placement}`]),role:"tooltip"},T(t.content),3)]))}}),e=y(c,[["__scopeId","data-v-65a1382d"]]);c.__docgenInfo={exportName:"default",displayName:"Tooltip",description:"",tags:{},props:[{name:"content",required:!0,type:{name:"string"}},{name:"placement",required:!1,type:{name:"union",elements:[{name:'"top"'},{name:'"bottom"'},{name:'"left"'},{name:'"right"'}]},defaultValue:{func:!1,value:"'top'"}}],slots:[{name:"default"}],sourceFiles:["/Users/welysson/projects/nordware-design-system/src/components/ui/molecules/Tooltip/Tooltip.vue"]};const S={title:"Design System/Molecules/Tooltip",component:e,tags:["autodocs"],argTypes:{content:{control:"text",description:"Texto do tooltip"},placement:{control:"select",options:["top","bottom","left","right"],description:"Posicionamento do tooltip"}},args:{content:"Tooltip text",placement:"top"}},o={render:t=>({components:{Tooltip:e},setup(){return{args:t}},template:`
      <div class="flex items-center justify-center p-12">
        <Tooltip v-bind="args">
          <button class="px-4 py-2 bg-primary text-white rounded-lg">
            Hover me
          </button>
        </Tooltip>
      </div>
    `}),args:{content:"This is a tooltip",placement:"top"}},n={render:t=>({components:{Tooltip:e},setup(){return{args:t}},template:`
      <div class="flex items-center justify-center p-12">
        <Tooltip v-bind="args">
          <button class="px-4 py-2 bg-gray-200 rounded-lg">
            Top Tooltip
          </button>
        </Tooltip>
      </div>
    `}),args:{content:"Tooltip on top",placement:"top"}},s={render:t=>({components:{Tooltip:e},setup(){return{args:t}},template:`
      <div class="flex items-center justify-center p-12">
        <Tooltip v-bind="args">
          <button class="px-4 py-2 bg-gray-200 rounded-lg">
            Bottom Tooltip
          </button>
        </Tooltip>
      </div>
    `}),args:{content:"Tooltip on bottom",placement:"bottom"}},r={render:t=>({components:{Tooltip:e},setup(){return{args:t}},template:`
      <div class="flex items-center justify-center p-12">
        <Tooltip v-bind="args">
          <button class="px-4 py-2 bg-gray-200 rounded-lg">
            Left Tooltip
          </button>
        </Tooltip>
      </div>
    `}),args:{content:"Tooltip on left",placement:"left"}},p={render:t=>({components:{Tooltip:e},setup(){return{args:t}},template:`
      <div class="flex items-center justify-center p-12">
        <Tooltip v-bind="args">
          <button class="px-4 py-2 bg-gray-200 rounded-lg">
            Right Tooltip
          </button>
        </Tooltip>
      </div>
    `}),args:{content:"Tooltip on right",placement:"right"}},a={render:()=>({components:{Tooltip:e},template:`
      <div class="grid grid-cols-2 gap-8 p-12">
        <div class="flex items-center justify-center">
          <Tooltip content="Top placement" placement="top">
            <button class="px-4 py-2 bg-gray-200 rounded-lg">Top</button>
          </Tooltip>
        </div>
        <div class="flex items-center justify-center">
          <Tooltip content="Bottom placement" placement="bottom">
            <button class="px-4 py-2 bg-gray-200 rounded-lg">Bottom</button>
          </Tooltip>
        </div>
        <div class="flex items-center justify-center">
          <Tooltip content="Left placement" placement="left">
            <button class="px-4 py-2 bg-gray-200 rounded-lg">Left</button>
          </Tooltip>
        </div>
        <div class="flex items-center justify-center">
          <Tooltip content="Right placement" placement="right">
            <button class="px-4 py-2 bg-gray-200 rounded-lg">Right</button>
          </Tooltip>
        </div>
      </div>
    `})},l={render:()=>({components:{Tooltip:e},template:`
      <div class="flex items-center justify-center p-12 gap-4">
        <Tooltip content="Edit" placement="top">
          <button class="p-2 hover:bg-gray-100 rounded">
            <div class="i-carbon-edit text-xl"></div>
          </button>
        </Tooltip>
        <Tooltip content="Delete" placement="top">
          <button class="p-2 hover:bg-gray-100 rounded">
            <div class="i-carbon-trash-can text-xl text-red-500"></div>
          </button>
        </Tooltip>
        <Tooltip content="Settings" placement="top">
          <button class="p-2 hover:bg-gray-100 rounded">
            <div class="i-carbon-settings text-xl"></div>
          </button>
        </Tooltip>
      </div>
    `})},i={render:t=>({components:{Tooltip:e},setup(){return{args:t}},template:`
      <div class="flex items-center justify-center p-12">
        <Tooltip v-bind="args">
          <button class="px-4 py-2 bg-primary text-white rounded-lg">
            Hover for more info
          </button>
        </Tooltip>
      </div>
    `}),args:{content:"This is a longer tooltip text that provides more detailed information",placement:"top"}};o.parameters={...o.parameters,docs:{...o.parameters?.docs,source:{originalSource:`{
  render: args => ({
    components: {
      Tooltip
    },
    setup() {
      return {
        args
      };
    },
    template: \`
      <div class="flex items-center justify-center p-12">
        <Tooltip v-bind="args">
          <button class="px-4 py-2 bg-primary text-white rounded-lg">
            Hover me
          </button>
        </Tooltip>
      </div>
    \`
  }),
  args: {
    content: 'This is a tooltip',
    placement: 'top'
  }
}`,...o.parameters?.docs?.source}}};n.parameters={...n.parameters,docs:{...n.parameters?.docs,source:{originalSource:`{
  render: args => ({
    components: {
      Tooltip
    },
    setup() {
      return {
        args
      };
    },
    template: \`
      <div class="flex items-center justify-center p-12">
        <Tooltip v-bind="args">
          <button class="px-4 py-2 bg-gray-200 rounded-lg">
            Top Tooltip
          </button>
        </Tooltip>
      </div>
    \`
  }),
  args: {
    content: 'Tooltip on top',
    placement: 'top'
  }
}`,...n.parameters?.docs?.source}}};s.parameters={...s.parameters,docs:{...s.parameters?.docs,source:{originalSource:`{
  render: args => ({
    components: {
      Tooltip
    },
    setup() {
      return {
        args
      };
    },
    template: \`
      <div class="flex items-center justify-center p-12">
        <Tooltip v-bind="args">
          <button class="px-4 py-2 bg-gray-200 rounded-lg">
            Bottom Tooltip
          </button>
        </Tooltip>
      </div>
    \`
  }),
  args: {
    content: 'Tooltip on bottom',
    placement: 'bottom'
  }
}`,...s.parameters?.docs?.source}}};r.parameters={...r.parameters,docs:{...r.parameters?.docs,source:{originalSource:`{
  render: args => ({
    components: {
      Tooltip
    },
    setup() {
      return {
        args
      };
    },
    template: \`
      <div class="flex items-center justify-center p-12">
        <Tooltip v-bind="args">
          <button class="px-4 py-2 bg-gray-200 rounded-lg">
            Left Tooltip
          </button>
        </Tooltip>
      </div>
    \`
  }),
  args: {
    content: 'Tooltip on left',
    placement: 'left'
  }
}`,...r.parameters?.docs?.source}}};p.parameters={...p.parameters,docs:{...p.parameters?.docs,source:{originalSource:`{
  render: args => ({
    components: {
      Tooltip
    },
    setup() {
      return {
        args
      };
    },
    template: \`
      <div class="flex items-center justify-center p-12">
        <Tooltip v-bind="args">
          <button class="px-4 py-2 bg-gray-200 rounded-lg">
            Right Tooltip
          </button>
        </Tooltip>
      </div>
    \`
  }),
  args: {
    content: 'Tooltip on right',
    placement: 'right'
  }
}`,...p.parameters?.docs?.source}}};a.parameters={...a.parameters,docs:{...a.parameters?.docs,source:{originalSource:`{
  render: () => ({
    components: {
      Tooltip
    },
    template: \`
      <div class="grid grid-cols-2 gap-8 p-12">
        <div class="flex items-center justify-center">
          <Tooltip content="Top placement" placement="top">
            <button class="px-4 py-2 bg-gray-200 rounded-lg">Top</button>
          </Tooltip>
        </div>
        <div class="flex items-center justify-center">
          <Tooltip content="Bottom placement" placement="bottom">
            <button class="px-4 py-2 bg-gray-200 rounded-lg">Bottom</button>
          </Tooltip>
        </div>
        <div class="flex items-center justify-center">
          <Tooltip content="Left placement" placement="left">
            <button class="px-4 py-2 bg-gray-200 rounded-lg">Left</button>
          </Tooltip>
        </div>
        <div class="flex items-center justify-center">
          <Tooltip content="Right placement" placement="right">
            <button class="px-4 py-2 bg-gray-200 rounded-lg">Right</button>
          </Tooltip>
        </div>
      </div>
    \`
  })
}`,...a.parameters?.docs?.source}}};l.parameters={...l.parameters,docs:{...l.parameters?.docs,source:{originalSource:`{
  render: () => ({
    components: {
      Tooltip
    },
    template: \`
      <div class="flex items-center justify-center p-12 gap-4">
        <Tooltip content="Edit" placement="top">
          <button class="p-2 hover:bg-gray-100 rounded">
            <div class="i-carbon-edit text-xl"></div>
          </button>
        </Tooltip>
        <Tooltip content="Delete" placement="top">
          <button class="p-2 hover:bg-gray-100 rounded">
            <div class="i-carbon-trash-can text-xl text-red-500"></div>
          </button>
        </Tooltip>
        <Tooltip content="Settings" placement="top">
          <button class="p-2 hover:bg-gray-100 rounded">
            <div class="i-carbon-settings text-xl"></div>
          </button>
        </Tooltip>
      </div>
    \`
  })
}`,...l.parameters?.docs?.source}}};i.parameters={...i.parameters,docs:{...i.parameters?.docs,source:{originalSource:`{
  render: args => ({
    components: {
      Tooltip
    },
    setup() {
      return {
        args
      };
    },
    template: \`
      <div class="flex items-center justify-center p-12">
        <Tooltip v-bind="args">
          <button class="px-4 py-2 bg-primary text-white rounded-lg">
            Hover for more info
          </button>
        </Tooltip>
      </div>
    \`
  }),
  args: {
    content: 'This is a longer tooltip text that provides more detailed information',
    placement: 'top'
  }
}`,...i.parameters?.docs?.source}}};const B=["Default","Top","Bottom","Left","Right","AllPlacements","OnIcon","LongText"];export{a as AllPlacements,s as Bottom,o as Default,r as Left,i as LongText,l as OnIcon,p as Right,n as Top,B as __namedExportsOrder,S as default};
