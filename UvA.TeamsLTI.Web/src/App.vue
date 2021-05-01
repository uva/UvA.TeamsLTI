<template>
  <Main />
</template>

<script lang="ts">
import { Options, Vue } from 'vue-class-component';
import Main from './components/Main.vue';

@Options({
  components: {
    Main,
  },
})
export default class App extends Vue {}
</script>

<style lang="scss">
@use 'variables' as *;

#app, input, button {
  font-family: Avenir, Helvetica, Arial, sans-serif;
  color: #2c3e50;
}

button {
  padding: 11px 17px;
  background: $primary-color;
  border: none;
  color: white;
  border-radius: 0.3rem;
  cursor: pointer;
  font-weight: bold;
  margin-right: 10px;

  &:disabled {
    background: #535353;
  }

  &:hover:not(:disabled) {
    background: $hover-color;
  }
}

button.button-secondary {
  background: $secondary-color;
  color: $secondary-text-color;

  &:hover:not(:disabled) {
    background: $secondary-hover-color;
  }
}

input[type='text'] {
  padding: 7px 10px;
  width: 250px;
}

input[type='checkbox'], input[type='radio'] {
  position: relative;
  top: 1px;
}

a {
  color: $primary-color;
  text-decoration: none;
}
</style>
