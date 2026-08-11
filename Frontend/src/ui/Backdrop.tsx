import css from "./Backdrop.module.css";


export function WaveBackdrop() {
   return <div className={css.wave} aria-hidden="true" />;
}

export function WindowCorners() {
   return (
      <div aria-hidden="true">
         <span className={`${css.corner} ${css.tl}`} />
         <span className={`${css.corner} ${css.tr}`} />
         <span className={`${css.corner} ${css.bl}`} />
         <span className={`${css.corner} ${css.br}`} />
      </div>
   );
}
