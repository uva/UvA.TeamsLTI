<template>
<transition name="fade">
    <div class="modal-mask">
        <div class="modal-wrapper">
            <div class="modal-container">
                <div class="lds-dual-ring"></div>
                <div>{{ text }}</div>
            </div>
        </div>
    </div>
</transition>
</template>

<script lang="ts">
import { Vue, Options } from 'vue-class-component';

@Options({
    props: {
        text: String
    }
})
export default class LoadingScreen extends Vue {
    text = "Saving, please wait";
}
</script>


<style lang="scss" scoped>
@use '../variables' as *;

.modal-mask {
  position: fixed;
  z-index: 9998;
  top: 0;
  left: 0;
  width: 100%;
  height: 100%;
  background-color: rgba(0, 0, 0, 0.5);
  display: table;
  transition: opacity 0.3s ease;
}

.modal-wrapper {
  display: table-cell;
  vertical-align: middle;
}

.modal-container {
  width: 150px;
  margin: 0px auto;
  padding: 25px 20px;
  background-color: #fff;
  border-radius: 10px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.33);
  transition: all 0.3s ease;
  overflow-y: auto;
  text-align: center;
}

.lds-dual-ring {
  display: inline-block;
  width: 70px;
  height: 80px;
}
.lds-dual-ring:after {
  content: " ";
  display: block;
  width: 50px;
  height: 50px;
  margin: 8px;
  border-radius: 50%;
  border: 6px solid $primary-color;
  border-color: $primary-color transparent $primary-color transparent;
  animation: lds-dual-ring 1.2s linear infinite;
}
@keyframes lds-dual-ring {
  0% {
    transform: rotate(0deg);
  }
  100% {
    transform: rotate(360deg);
  }
}

</style>