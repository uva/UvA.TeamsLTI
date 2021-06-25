<template>
    <a @focus="isOpen = true" tabindex="-1" @focusout="isOpen = false">
        <svg aria-hidden="true" focusable="false" data-prefix="fas" data-icon="info-circle" class="svg-inline--fa fa-info-circle fa-w-16" role="img" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 512 512"><path fill="currentColor" d="M256 8C119.043 8 8 119.083 8 256c0 136.997 111.043 248 248 248s248-111.003 248-248C504 119.083 392.957 8 256 8zm0 110c23.196 0 42 18.804 42 42s-18.804 42-42 42-42-18.804-42-42 18.804-42 42-42zm56 254c0 6.627-5.373 12-12 12h-88c-6.627 0-12-5.373-12-12v-24c0-6.627 5.373-12 12-12h12v-64h-12c-6.627 0-12-5.373-12-12v-24c0-6.627 5.373-12 12-12h64c6.627 0 12 5.373 12 12v100h12c6.627 0 12 5.373 12 12v24z"></path></svg>
        <div class="tip" @click="isOpen = false" v-if="isOpen"><slot></slot></div>
    </a>
</template>

<script lang="ts">
import { Vue } from 'vue-class-component';

export default class Tooltip extends Vue {
    isOpen = false;
}
</script>


<style lang="scss" scoped>
@use '../variables' as *;

svg {
    width: 16px;
    position: relative;
    top: 2px;
    left: 3px;
}

a {
    cursor: pointer;
}

.tip {
    position: absolute;
    left: 15px;
    width: 250px;
    border: 1px solid #aaa;
    background: white;
    padding: 8px;
    z-index: 10;
    color: #333;
    font-size: small;
    box-shadow: 0px 0px 5px #333;
}

</style>